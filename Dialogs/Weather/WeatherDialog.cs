using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BasicBot.Dialogs.Weather.Models;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace BasicBot.Dialogs.Weather
{
   
    public class WeatherDialog : ComponentDialog
    {
        // User state for greeting dialog
        private const string WeatherStateProperty = "greetingState";
        private const string CityValue = "weatherCity";

        // Prompts names
        private const string CityPrompt = "cityPrompt";
        private const string ForecastTypePrompt = "forecastTypePrompt";

        // Minimum length requirements for city and type
        private const int CityLengthMinValue = 3;
        private const int ForecastTypeLengthMinValue = 3;

        // Dialog IDs
        private const string ProfileDialog = "profileDialog";

        // Weather API
        private const string WeatherApi = "https://api.openweathermap.org/data/2.5/";
        private const string WeatherApiCurrent = "https://api.openweathermap.org/data/2.5/weather?q=";
        private const string WeatherApiForecast = "https://api.openweathermap.org/data/2.5/forecast?q=";
        private const string WeatherApiSettings = "&units=metric&APPID=f2b3af247004bf8543677aa8cb2a20de";
        private const string BadRequest = "Bad request";
        private const string ForecastType = "forecast";
        private const string CurrentType = "weather";

        /// <summary>
        /// Initializes a new instance of the <see cref="WeatherDialog"/> class.
        /// </summary>
        /// <param name="botServices">Connected services used in processing.</param>
        /// <param name="weatherStateAccessor">The <see cref="WeatherState"/> for storing properties at user-scope.</param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> that enables logging and tracing.</param>
        public WeatherDialog(IStatePropertyAccessor<WeatherState> weatherStateAccessor, ILoggerFactory loggerFactory)
            : base(nameof(WeatherDialog))
        {
            WeatherAccessor = weatherStateAccessor ?? throw new ArgumentNullException(nameof(weatherStateAccessor));

            // Add control flow dialogs
            var waterfallSteps = new WaterfallStep[]
            {
                    InitializeStateStepAsync,
                    PromptForCityStepAsync,
                    PromptForForecastTypeStepAsync,
                    DisplayWeatherStateStepAsync,
            };
            AddDialog(new WaterfallDialog(ProfileDialog, waterfallSteps));
            AddDialog(new TextPrompt(CityPrompt, ValidateCity));
            AddDialog(new TextPrompt(ForecastTypePrompt, ValidateForecastType));
        }

        public IStatePropertyAccessor<WeatherState> WeatherAccessor { get; }

        private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var weatherState = await WeatherAccessor.GetAsync(stepContext.Context, () => null);
            if (weatherState == null)
            {
                var weatherStateOpt = stepContext.Options as WeatherState;
                if (weatherStateOpt != null)
                {
                    await WeatherAccessor.SetAsync(stepContext.Context, weatherStateOpt);
                }
                else
                {
                    await WeatherAccessor.SetAsync(stepContext.Context, new WeatherState());
                }
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> PromptForCityStepAsync(
                                                        WaterfallStepContext stepContext,
                                                        CancellationToken cancellationToken)
        {
            // Save name, if prompted.
            var weatherState = await WeatherAccessor.GetAsync(stepContext.Context);

            if (string.IsNullOrWhiteSpace(weatherState.City))
            {
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = $"Hello, what city do you want to get weather in?",
                    },
                };
                return await stepContext.PromptAsync(CityPrompt, opts);
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }


        private async Task<DialogTurnResult> PromptForForecastTypeStepAsync(
                                                       WaterfallStepContext stepContext,
                                                       CancellationToken cancellationToken)
        {
            var weatherState = await WeatherAccessor.GetAsync(stepContext.Context);

            var lowerCaseCity = stepContext.Result as string;
            if (string.IsNullOrWhiteSpace(weatherState.City) &&
                !string.IsNullOrWhiteSpace(lowerCaseCity))
            {
                // capitalize and set city
                weatherState.City = char.ToUpper(lowerCaseCity[0]) + lowerCaseCity.Substring(1);
                await WeatherAccessor.SetAsync(stepContext.Context, weatherState);
            }

            if (string.IsNullOrWhiteSpace(weatherState.ForecastType))
            {

                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        SuggestedActions = new SuggestedActions() { Actions = new List<CardAction>()
                            {
                                new CardAction() { Title = "Current", Type = ActionTypes.ImBack, Value = "current" },
                                new CardAction() { Title = "Forecast", Type = ActionTypes.ImBack, Value = "forecast" },
                            },
                        },
                        Text = $"Choose weather type:",
                    },
                };
                return await stepContext.PromptAsync(ForecastTypePrompt, opts);
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }

        private async Task<DialogTurnResult> DisplayWeatherStateStepAsync(
                                                    WaterfallStepContext stepContext,
                                                    CancellationToken cancellationToken)
        {
            var weatherState = await WeatherAccessor.GetAsync(stepContext.Context);

            var lowerCaseForecast = stepContext.Result as string;
            if (string.IsNullOrWhiteSpace(weatherState.ForecastType) &&
                !string.IsNullOrWhiteSpace(lowerCaseForecast))
            {
                // capitalize and set forecast type
                weatherState.ForecastType = char.ToUpper(lowerCaseForecast[0]) + lowerCaseForecast.Substring(1);
                await WeatherAccessor.SetAsync(stepContext.Context, weatherState);
            }

            return await ShowWeather(stepContext);
        }

        /// <summary>
        /// Validator function to verify if city meets required constraints.
        /// </summary>
        /// <param name="promptContext">Context for this prompt.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        private async Task<bool> ValidateCity(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            // Validate that the user entered a minimum lenght for their name
            var value = promptContext.Recognized.Value?.Trim() ?? string.Empty;
            if (value.Length >= CityLengthMinValue)
            {
                promptContext.Recognized.Value = value;
                return true;
            }
            else
            {
                await promptContext.Context.SendActivityAsync($"City names needs to be at least `{CityLengthMinValue}` characters long.");
                return false;
            }
        }

        /// <summary>
        /// Validator function to verify if city meets required constraints.
        /// </summary>
        /// <param name="promptContext">Context for this prompt.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        private async Task<bool> ValidateForecastType(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            // Validate that the user entered a minimum lenght for their name
            var value = promptContext.Recognized.Value?.Trim() ?? string.Empty;
            if (value.Length >= CityLengthMinValue)
            {
                promptContext.Recognized.Value = value;
                return true;
            }
            else
            {
                await promptContext.Context.SendActivityAsync($"Forecast type needs to be at least `{ForecastTypeLengthMinValue}` characters long.");
                return false;
            }
        }

        // Helper function to greet user with information in GreetingState.
        private async Task<DialogTurnResult> ShowWeather(WaterfallStepContext stepContext)
        {
            var context = stepContext.Context;
            var weatherState = await WeatherAccessor.GetAsync(context);
            var choosenForecastType = ForecastType;
            if (string.IsNullOrWhiteSpace(weatherState.ForecastType) || weatherState.ForecastType == "Current")
            {
                choosenForecastType = CurrentType;
            }

            if (choosenForecastType == CurrentType)
            {
                var currentWeather = GetCurrentWeather(weatherState.City);
                if (currentWeather.RequestType == BadRequest)
                {
                    await WeatherAccessor.SetAsync(stepContext.Context, new WeatherState());
                    await context.SendActivityAsync($"Wrong city name.");
                }
                else
                {
                    var reply = context.Activity.CreateReply();

                    // Cards are sent as Attachments in the Bot Framework.
                    // So we need to create a list of attachments on the activity.
                    reply.Attachments = new List<Attachment>();
                    reply.Attachments.Add(GetHeroCard(weatherState.City, currentWeather).ToAttachment());

                    // Send the card(s) to the user as an attachment to the activity
                    await context.SendActivityAsync(reply);
                }
            }
            else
            {
                var weatherForecast = GetWeatherForecast(weatherState.City);
                if (weatherForecast.RequestType == BadRequest)
                {
                    await WeatherAccessor.SetAsync(stepContext.Context, new WeatherState());
                    await context.SendActivityAsync($"Wrong city name.");
                }
                else
                {
                    var reply = context.Activity.CreateReply();

                    // Cards are sent as Attachments in the Bot Framework.
                    // So we need to create a list of attachments on the activity.
                    reply.Attachments = new List<Attachment>();
                    var forecastAttachments = GetForecastCards(weatherState.City, weatherForecast);
                    foreach (var item in forecastAttachments)
                    {
                        reply.Attachments.Add(item.ToAttachment());
                    }

                    // Send the card(s) to the user as an attachment to the activity
                    await context.SendActivityAsync(reply);
                }
            }

            await WeatherAccessor.SetAsync(stepContext.Context, new WeatherState());

            return await stepContext.EndDialogAsync();
        }

        private CurrentWeather GetCurrentWeather(string city)
        {
            //https://api.openweathermap.org/data/2.5/weather?q=Rivne&units=metric&APPID=f2b3af247004bf8543677aa8cb2a20de"));
            CurrentWeather weatherResult = null;
            try
            {
                HttpWebRequest WebReq = (HttpWebRequest)WebRequest.
                Create(string.Format(WeatherApiCurrent + city + WeatherApiSettings));

                WebReq.Method = "GET";

                HttpWebResponse WebResp = (HttpWebResponse)WebReq.GetResponse();

                string jsonString;
                using (Stream stream = WebResp.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(stream, System.Text.Encoding.UTF8);
                    jsonString = reader.ReadToEnd();
                }

                weatherResult = CurrentWeather.FromJson(jsonString);
            }
            catch (Exception e)
            {
                weatherResult = new CurrentWeather() { RequestType = BadRequest};
            }

            return weatherResult;
        }

        private WeatherForecast GetWeatherForecast(string city)
        {
            //https://api.openweathermap.org/data/2.5/forecast?q=Rivne&units=metric&APPID=f2b3af247004bf8543677aa8cb2a20de"));
            WeatherForecast weatherResult = null;
            try
            {
                HttpWebRequest WebReq = (HttpWebRequest)WebRequest.
                Create(string.Format(WeatherApiForecast + city + WeatherApiSettings));

                WebReq.Method = "GET";

                HttpWebResponse WebResp = (HttpWebResponse)WebReq.GetResponse();

                string jsonString;
                using (Stream stream = WebResp.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(stream, System.Text.Encoding.UTF8);
                    jsonString = reader.ReadToEnd();
                }

                weatherResult = WeatherForecast.FromJson(jsonString);
                weatherResult.RequestType = "OK";
            }
            catch (Exception e)
            {
                weatherResult = new WeatherForecast() { RequestType = BadRequest};
            }

            return weatherResult;
        }

        /// <summary>
        /// Creates a <see cref="ThumbnailCard"/>.
        /// </summary>
        /// <returns>A <see cref="ThumbnailCard"/> the user can view and/or interact with.</returns>
        /// <remarks>Related types <see cref="CardImage"/>, <see cref="CardAction"/>,
        /// and <see cref="ActionTypes"/>.</remarks>
        private ThumbnailCard GetHeroCard(string city, CurrentWeather weather)
        {
            var heroCard = new ThumbnailCard
            {
                Title = $"{city}:",
                Subtitle = DateTime.Now.ToShortTimeString(),
                Text = $"temperature {(int)weather.Main.Temp} °C" +
                    $"\nhumidity {weather.Main.Humidity} %",
                Images = new List<CardImage> { new CardImage("http://openweathermap.org/img/w/" + weather.Weather[0].Icon + ".png") },
                Buttons = new List<CardAction> { new CardAction(ActionTypes.OpenUrl, "More information", value: "https://openweathermap.org/find?q=" + city) },
            };

            return heroCard;
        }

        /// <summary>
        /// Creates a <see cref="ThumbnailCard"/>.
        /// </summary>
        /// <returns>A <see cref="ThumbnailCard"/> the user can view and/or interact with.</returns>
        /// <remarks>Related types <see cref="CardImage"/>, <see cref="CardAction"/>,
        /// and <see cref="ActionTypes"/>.</remarks>
        private List<ThumbnailCard> GetForecastCards(string city, WeatherForecast weatherForecast)
        {
            var weatherItems = new List<ThumbnailCard>();
            int maxItemsCount = 3;
            int startNum = 0;
            foreach (var item in weatherForecast.List)
            {
                if (startNum < maxItemsCount)
                {
                    string imageUrl = "http://openweathermap.org/img/w/" + item.Weather[0].Icon + ".png";
                    string temp = item.Main.Temp > 0 ? "+" + ((int)item.Main.Temp).ToString() : ((int)item.Main.Temp).ToString();
                    var weatherCard = new ThumbnailCard
                    {
                        Title = $"{city} {item.DtTxt.DateTime.ToShortDateString()}:",
                        Subtitle = item.DtTxt.DateTime.ToShortTimeString(),
                        Text = $"temperature {temp} °C" +
                    $"\nhumidity {item.Main.Humidity} %",
                        Images = new List<CardImage> { new CardImage("http://openweathermap.org/img/w/" + item.Weather[0].Icon + ".png") },
                        Buttons = new List<CardAction> { new CardAction(ActionTypes.OpenUrl, "More information", value: "https://openweathermap.org/find?q=" + city) },
                    };

                    weatherItems.Add(weatherCard);
                }
                ++startNum;
            }

            return weatherItems;
        }

        /// <summary>
        /// Creates a <see cref="ReceiptCard"/>.
        /// </summary>
        /// <returns>A <see cref="ReceiptCard"/> the user can view and/or interact with.</returns>
        /// <remarks>Related types <see cref="CardImage"/>, <see cref="CardAction"/>,
        /// <see cref="ActionTypes"/>, <see cref="ReceiptItem"/>, and <see cref="Fact"/>.</remarks>
        private ReceiptCard GetReceiptCard(string city, WeatherForecast weatherForecast)
        {
            var weatherItems = new List<ReceiptItem>();
            int maxItemsCount = 3;
            int startNum = 0;
            foreach (var item in weatherForecast.List)
            {
                if (startNum < maxItemsCount)
                {
                    string imageUrl = "http://openweathermap.org/img/w/" + item.Weather[0].Icon + ".png";
                    string temp = item.Main.Temp > 0 ? "+" + ((int)item.Main.Temp).ToString() : ((int)item.Main.Temp).ToString();
                    weatherItems.Add(new ReceiptItem(
                        item.DtTxt.DateTime.ToShortTimeString(),
                        price: temp,
                        image: new CardImage(url: imageUrl)));
                }
                ++startNum;
            }

            var receiptCard = new ReceiptCard
            {
                Title = $"Weather forecast in {city}:",
                Items = weatherItems,
                Buttons = new List<CardAction>
                {
                    new CardAction(
                        ActionTypes.ImBack,
                        "More information",
                        "https://account.windowsazure.com/content/6.10.1.38-.8225.160809-1618/aux-pre/images/offer-icon-freetrial.png",
                        "https://openweathermap.org/find?q=" + city),
                },
            };

            return receiptCard;
        }
    }
}

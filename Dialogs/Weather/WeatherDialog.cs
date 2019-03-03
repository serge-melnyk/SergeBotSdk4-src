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

        // Minimum length requirements for city and name
        private const int CityLengthMinValue = 3;

        // Dialog IDs
        private const string ProfileDialog = "profileDialog";

        // Weather API
        private const string WeatherApi = "https://api.openweathermap.org/data/2.5/";
        private const string WeatherApiSettings = "&units=metric&APPID=f2b3af247004bf8543677aa8cb2a20de";
        private const string BadRequest = "Bad request";
        private const string ForecastType = "forecast";
        private const string CurrentType = "weather";

        /// <summary>
        /// Initializes a new instance of the <see cref="WeatherDialog"/> class.
        /// </summary>
        /// <param name="botServices">Connected services used in processing.</param>
        /// <param name="botState">The <see cref="UserState"/> for storing properties at user-scope.</param>
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
                        SuggestedActions = new SuggestedActions() {  Actions = new List<CardAction>()
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
                await promptContext.Context.SendActivityAsync($"City names needs to be at least `{CityLengthMinValue}` characters long.");
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

            // Display their profile information and end dialog.
            var weatherResult = GetCurrentWeather(weatherState.City, choosenForecastType);
            if (weatherResult == BadRequest)
            {
                await WeatherAccessor.SetAsync(stepContext.Context, new WeatherState());
                await context.SendActivityAsync($"Wrong city name.");
            }
            else
            {
                await context.SendActivityAsync($"The weather in your city {weatherState.City}:" +
                $"\n{GetCurrentWeather(weatherState.City, choosenForecastType)}");
            }

            return await stepContext.EndDialogAsync();
        }

        private string GetCurrentWeather(string city, string weatherType)
        {
            //https://api.openweathermap.org/data/2.5/weather?q=Rivne&units=metric&APPID=f2b3af247004bf8543677aa8cb2a20de"));
            string weatherResult = string.Empty;
            try
            {
                HttpWebRequest WebReq = (HttpWebRequest)WebRequest.
                Create(string.Format(WeatherApi + weatherType + "?q=" + city + WeatherApiSettings));

                WebReq.Method = "GET";

                HttpWebResponse WebResp = (HttpWebResponse)WebReq.GetResponse();

                string jsonString;
                using (Stream stream = WebResp.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(stream, System.Text.Encoding.UTF8);
                    jsonString = reader.ReadToEnd();
                }

                if (weatherType == CurrentType)
                {
                    CurrentWeather weather = CurrentWeather.FromJson(jsonString);
                    weatherResult = $"temperature {(int)weather.Main.Temp} °C" +
                    $"\nhumidity {weather.Main.Humidity} %";
                }
                else
                {
                    WeatherForecast weatherForecast = WeatherForecast.FromJson(jsonString);
                    foreach (var item in weatherForecast.List)
                    {
                        weatherResult += $"\n**{item.DtTxt}**" +
                            $"\ntemperature {(int)item.Main.Temp} °C" +
                            $"\nhumidity {item.Main.Humidity} %";
                    }
                }

            }
            catch (Exception e)
            {
                weatherResult = BadRequest;
            }

            return weatherResult;
        }
    }
}

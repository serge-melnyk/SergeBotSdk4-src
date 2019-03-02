using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
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

        // Minimum length requirements for city and name
        private const int CityLengthMinValue = 3;

        // Dialog IDs
        private const string ProfileDialog = "profileDialog";

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
                    //PromptForNameStepAsync,
                    PromptForCityStepAsync,
                    DisplayWeatherStateStepAsync,
            };
            AddDialog(new WaterfallDialog(ProfileDialog, waterfallSteps));
            //AddDialog(new TextPrompt(NamePrompt, ValidateName));
            AddDialog(new TextPrompt(CityPrompt, ValidateCity));
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
            //var lowerCaseName = stepContext.Result as string;
            //if (string.IsNullOrWhiteSpace(greetingState.Name) && lowerCaseName != null)
            //{
            //    // Capitalize and set name.
            //    greetingState.Name = char.ToUpper(lowerCaseName[0]) + lowerCaseName.Substring(1);
            //    await UserProfileAccessor.SetAsync(stepContext.Context, greetingState);
            //}

            if (string.IsNullOrWhiteSpace(weatherState.City))
            {
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        //Text = $"Hello {greetingState.Name}, what city do you live in?",
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

        private async Task<DialogTurnResult> DisplayWeatherStateStepAsync(
                                                    WaterfallStepContext stepContext,
                                                    CancellationToken cancellationToken)
        {
            // Save city, if prompted.
            var weatherState = await WeatherAccessor.GetAsync(stepContext.Context);

            var lowerCaseCity = stepContext.Result as string;
            if (string.IsNullOrWhiteSpace(weatherState.City) &&
                !string.IsNullOrWhiteSpace(lowerCaseCity))
            {
                // capitalize and set city
                weatherState.City = char.ToUpper(lowerCaseCity[0]) + lowerCaseCity.Substring(1);
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

        // Helper function to greet user with information in GreetingState.
        private async Task<DialogTurnResult> ShowWeather(WaterfallStepContext stepContext)
        {
            var context = stepContext.Context;
            var weatherState = await WeatherAccessor.GetAsync(context);

            // Display their profile information and end dialog.
            await context.SendActivityAsync($"The weather in your city {weatherState.City}:");
            //await context.SendActivityAsync($"Hi {greetingState.Name}, from {greetingState.City}, nice to meet you!");
            return await stepContext.EndDialogAsync();
        }
    }
}

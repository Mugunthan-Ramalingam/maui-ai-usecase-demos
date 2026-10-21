using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace SchedulerAISample
{
	/// <summary>
	/// Provides Azure OpenAI integration for generating AI-powered project timelines.
	/// </summary>
	public class AzureOpenAIPlannerService : IAIPlannerService
	{
		private readonly AzureOpenAIOptions _options;

		private ChatClient? _chatClient;

		private static readonly JsonSerializerOptions JsonOptions =
			new()
			{
				PropertyNameCaseInsensitive = true
			};

		/// <summary>
		/// Initializes a new instance of the <see cref="AzureOpenAIPlannerService"/> class.
		/// </summary>
		public AzureOpenAIPlannerService()
		{
			_options = new AzureOpenAIOptions();
		}

		/// <summary>
		/// Generates a project timeline based on the user's goal using Azure OpenAI.
		/// </summary>
		/// <param name="userInput">The project goal provided by the user.</param>
		/// <param name="cancellationToken">A token used to cancel the operation.</param>
		/// <returns>
		/// An <see cref="AIPlannerResponse"/> containing the generated project plan; otherwise, <see langword="null"/>.
		/// </returns>
		public async Task<AIPlannerResponse?> GeneratePlanAsync(string userInput, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(userInput))
			{
				return null;
			}

			string? validationError = _options.ValidateCredentials();

			if (!string.IsNullOrWhiteSpace(validationError))
			{
				throw new InvalidOperationException(validationError);
			}

			EnsureChatClient();

			string currentDate = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

			string systemPrompt =
				$$"""
            You are an AI Project Planner.

            Convert the user's high-level goal into a practical timeline of tasks.

            Today's date is:

            {{currentDate}}

            Generate between 5 and 7 meaningful tasks.

            Each task must contain:

            TaskName
            StartDate
            EndDate
            Description

            RULES:

            1. Tasks must be relevant to the user's goal.

            2. Arrange tasks in a logical chronological order.

            3. Assign reasonable dates and durations.

            4. Do not create dates in the past unless the user explicitly requests past dates.

            5. Interpret relative dates using today's date.

            Examples:

            "next month"
            "next week"
            "in 30 days"
            "this weekend"

            6. StartDate and EndDate must use this exact format:

            yyyy-MM-ddTHH:mm:ss

            7. EndDate must always be later than StartDate.

            8. Do not generate unnecessary tasks.

            9. Do not return Markdown.

            10. Do not return explanations outside JSON.

            11. Return ONLY valid JSON.

            Expected format:

            {
                "PlanTitle": "Company Event Plan",
                "Tasks":
                [
                    {
                        "TaskName": "Venue Booking",
                        "StartDate": "2026-11-02T09:00:00",
                        "EndDate": "2026-11-02T10:00:00",
                        "Description":
                            "Research and confirm the event venue."
                    }
                ]
            }
            """;

			string userPrompt =
				$$"""
            Create a project timeline for this goal:

            {{userInput}}
            """;

			List<ChatMessage> messages =
			[
				new SystemChatMessage(systemPrompt),
			new UserChatMessage(userPrompt)
			];

			ChatCompletionOptions options =
				new()
				{
					ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
				};

			try
			{
				ClientResult<ChatCompletion> result =
					await _chatClient!.CompleteChatAsync(
						messages,
						options,
						cancellationToken);

				string json =
					string.Concat(
						result.Value.Content.Select(
							part => part.Text ?? string.Empty));

				Debug.WriteLine("===== AI PLANNER RESPONSE =====");
				Debug.WriteLine(json);

				if (string.IsNullOrWhiteSpace(json))
				{
					return null;
				}

				AIPlannerResponse? response =
					JsonSerializer.Deserialize<AIPlannerResponse>(
						json,
						JsonOptions);

				if (response?.Tasks == null ||
					response.Tasks.Count == 0)
				{
					return null;
				}

				NormalizeTasks(response);

				return response;
			}
			catch (ClientResultException ex)
			{
				Debug.WriteLine($"Azure AI Error: {ex}");
				throw;
			}
		}

		/// <summary>
		/// Creates and initializes the Azure OpenAI chat client if it has not already been created.
		/// </summary>
		private void EnsureChatClient()
		{
			if (_chatClient != null)
			{
				return;
			}

			AzureOpenAIClient client =
				new(
					new Uri(_options.EndpointValue),
					new AzureKeyCredential(_options.KeyValue));

			_chatClient =
				client.GetChatClient(
					_options.DeploymentNameValue);
		}

		/// <summary>
		/// Normalizes and validates the generated tasks before they are displayed in the Scheduler.
		/// </summary>
		/// <param name="response">The generated AI planner response.</param>
		private static void NormalizeTasks(AIPlannerResponse response)
		{
			foreach (AIPlannerTask task in response.Tasks)
			{
				task.TaskName =
					task.TaskName?.Trim() ??
					"Task";

				task.Description =
					task.Description?.Trim() ??
					string.Empty;

				if (task.StartDate == default)
				{
					task.StartDate =
						DateTime.Today
							.AddDays(1)
							.AddHours(9);
				}

				if (task.EndDate <= task.StartDate)
				{
					task.EndDate =
						task.StartDate
							.AddHours(1);
				}
			}

			response.Tasks =
				response.Tasks
					.OrderBy(task => task.StartDate)
					.ToList();
		}
	}
}

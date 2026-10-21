using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Syncfusion.Maui.Scheduler;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;

namespace SchedulerAISample
{
	/// <summary>
	/// Represents the view model that manages AI-powered project planning and Scheduler interactions.
	/// </summary>
	public partial class SchedulerViewModel : ObservableObject
	{
		private readonly IAIPlannerService _plannerService;
		private CancellationTokenSource? _cancellationTokenSource;

		private string userInput = string.Empty;
		private string planTitle = "AI Project Timeline";
		private bool isBusy;
		private bool isReviewVisible;
		private bool showScheduler;
		private DateTime displayDate = DateTime.Today;

		/// <summary>
		/// Initializes a new instance of the <see cref="SchedulerViewModel"/> class.
		/// </summary>
		public SchedulerViewModel()
		{
			_plannerService = new AzureOpenAIPlannerService();
			Appointments = new ObservableCollection<SchedulerAppointment>();
			PendingTasks = new ObservableCollection<AIPlannerTask>();

			PendingTasks.CollectionChanged += PendingTasks_CollectionChanged;
		}

		/// <summary>
		/// Gets or sets the user-provided project goal.
		/// </summary>
		public string UserInput
		{
			get => userInput;
			set => SetProperty(ref userInput, value);
		}

		/// <summary>
		/// Gets or sets the project plan title.
		/// </summary>
		public string PlanTitle
		{
			get => planTitle;
			set => SetProperty(ref planTitle, value);
		}

		/// <summary>
		/// Gets or sets a value indicating whether timeline generation is in progress.
		/// </summary>
		public bool IsBusy
		{
			get => isBusy;
			set => SetProperty(ref isBusy, value);
		}

		/// <summary>
		/// Gets or sets a value indicating whether the review panel is visible.
		/// </summary>
		public bool IsReviewVisible
		{
			get => isReviewVisible;
			set => SetProperty(ref isReviewVisible, value);
		}

		/// <summary>
		/// Gets or sets a value indicating whether the Scheduler is visible.
		/// </summary>
		public bool ShowScheduler
		{
			get => showScheduler;
			set => SetProperty(ref showScheduler, value);
		}

		/// <summary>
		/// Gets or sets the date displayed by the Scheduler.
		/// </summary>
		public DateTime DisplayDate
		{
			get => displayDate;
			set => SetProperty(ref displayDate, value);
		}

		/// <summary>
		/// Gets the collection of Scheduler appointments.
		/// </summary>
		public ObservableCollection<SchedulerAppointment> Appointments { get; }

		/// <summary>
		/// Gets the collection of AI-generated tasks awaiting review.
		/// </summary>
		public ObservableCollection<AIPlannerTask> PendingTasks { get; }

		/// <summary>
		/// Generates a project timeline from the user-provided goal using Azure OpenAI.
		/// </summary>
		[RelayCommand]
		private async Task GenerateTimelineAsync()
		{
			if (string.IsNullOrWhiteSpace(UserInput))
			{
				return;
			}

			IsBusy = true;
			ShowScheduler = false;
			IsReviewVisible = false;

			await Task.Yield();

			try
			{
				_cancellationTokenSource?.Cancel();
				_cancellationTokenSource = new CancellationTokenSource();

				AIPlannerResponse? response = await _plannerService.GeneratePlanAsync(UserInput, _cancellationTokenSource.Token);

				if (response?.Tasks == null || response.Tasks.Count == 0)
				{
					return;
				}

				PlanTitle = string.IsNullOrWhiteSpace(response.PlanTitle)
					? "AI Project Timeline"
					: response.PlanTitle;

				PendingTasks.Clear();

				foreach (AIPlannerTask task in response.Tasks)
				{
					PendingTasks.Add(new AIPlannerTask
					{
						TaskName = task.TaskName,
						StartDate = task.StartDate,
						EndDate = task.EndDate,
						Description = task.Description
					});
				}

				IsReviewVisible = true;
				ShowScheduler = false;
			}
			catch (OperationCanceledException)
			{
				Debug.WriteLine("Timeline generation cancelled.");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Timeline generation failed: {ex}");
			}
			finally
			{
				IsBusy = false;
			}
		}

		/// <summary>
		/// Applies the reviewed tasks and displays them in the Scheduler.
		/// </summary>
		[RelayCommand]
		private void ApplySchedule()
		{
			if (PendingTasks.Count == 0)
			{
				return;
			}

			RefreshScheduleFromPendingTasks();

			ShowScheduler = true;
			IsReviewVisible = false;
		}

		/// <summary>
		/// Switches back to the timeline review mode.
		/// </summary>
		[RelayCommand]
		private void EditSchedule()
		{
			if (PendingTasks.Count == 0)
			{
				return;
			}

			ShowScheduler = false;
			IsReviewVisible = true;
		}

		/// <summary>
		/// Removes the selected task from the generated timeline.
		/// </summary>
		/// <param name="task">The task to remove.</param>
		[RelayCommand]
		private void RemoveTask(AIPlannerTask? task)
		{
			if (task == null)
			{
				return;
			}

			PendingTasks.Remove(task);
		}

		/// <summary>
		/// Adds a new task to the generated project timeline.
		/// </summary>
		[RelayCommand]
		private void AddTask()
		{
			DateTime baseDate = PendingTasks.Count > 0
				? PendingTasks.Max(t => t.EndDate).AddDays(1)
				: DateTime.Today.AddDays(1).AddHours(9);

			PendingTasks.Add(new AIPlannerTask
			{
				TaskName = $"New task {PendingTasks.Count + 1}",
				StartDate = baseDate,
				EndDate = baseDate.AddHours(2),
				Description = string.Empty
			});
		}

		/// <summary>
		/// Discards the current timeline and resets the view model state.
		/// </summary>
		[RelayCommand]
		private void DiscardChanges()
		{
			Appointments.Clear();
			PendingTasks.Clear();
			IsReviewVisible = false;
			ShowScheduler = false;
			PlanTitle = "AI Project Timeline";
		}

		/// <summary>
		/// Refreshes the Scheduler appointments based on the reviewed task collection.
		/// </summary>
		private void RefreshScheduleFromPendingTasks()
		{
			Appointments.Clear();

			int colorIndex = 0;

			foreach (AIPlannerTask task in PendingTasks)
			{
				SchedulerAppointment appointment = new()
				{
					Subject = task.TaskName,
					StartTime = task.StartDate,
					EndTime = task.EndDate,
					Notes = task.Description,
					Background = new SolidColorBrush(AppointmentPalette.GetColor(colorIndex)),
					TextColor = Colors.White
				};

				Appointments.Add(appointment);
				colorIndex++;
			}

			DisplayDate = PendingTasks.Min(task => task.StartDate);
		}

		/// <summary>
		/// Handles changes to the task collection and updates the Scheduler.
		/// </summary>
		private void PendingTasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.OldItems != null)
			{
				foreach (AIPlannerTask task in e.OldItems)
				{
					task.PropertyChanged -= PendingTask_PropertyChanged;
				}
			}

			if (e.NewItems != null)
			{
				foreach (AIPlannerTask task in e.NewItems)
				{
					task.PropertyChanged += PendingTask_PropertyChanged;
				}
			}

			if (PendingTasks.Count > 0)
			{
				RefreshScheduleFromPendingTasks();
			}
		}

		/// <summary>
		/// Handles changes to individual task properties and updates the Scheduler.
		/// </summary>
		private void PendingTask_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (PendingTasks.Count == 0)
			{
				return;
			}

			RefreshScheduleFromPendingTasks();
		}
	}
}

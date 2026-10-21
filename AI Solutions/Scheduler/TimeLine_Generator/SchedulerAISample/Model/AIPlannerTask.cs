using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace SchedulerAISample
{
	public class AIPlannerTask : ObservableObject
	{
		private string taskName = string.Empty;
		private DateTime startDate;
		private DateTime endDate;
		private string description = string.Empty;

		public string TaskName
		{
			get => taskName;
			set => SetProperty(ref taskName, value);
		}

		public DateTime StartDate
		{
			get => startDate;
			set
			{
				if (SetProperty(ref startDate,value) && endDate < value)
				{
					EndDate = value.AddHours(1);
				}
			}
		}

		public DateTime EndDate
		{
			get => endDate;
			set
			{
				if (value < startDate)
				{
					value =startDate.AddHours(1);
				}
				SetProperty(ref endDate, value);
			}
		}

		public string Description
		{
			get => description;
			set => SetProperty(ref description, value);
		}
	}
}

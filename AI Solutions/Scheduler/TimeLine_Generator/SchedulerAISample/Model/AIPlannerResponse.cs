using System;
using System.Collections.Generic;
using System.Text;

namespace SchedulerAISample
{
	public class AIPlannerResponse
	{
		public string PlanTitle { get; set; } = string.Empty;
		public List<AIPlannerTask> Tasks { get; set; } = new();
	}
}

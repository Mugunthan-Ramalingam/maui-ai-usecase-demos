using System;
using System.Collections.Generic;
using System.Text;

namespace SchedulerAISample
{
	public interface IAIPlannerService
	{
		Task<AIPlannerResponse?> GeneratePlanAsync(string userInput, CancellationToken cancellationToken = default);
	}
}

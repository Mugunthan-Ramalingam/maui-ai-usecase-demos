using Microsoft.Maui.Graphics;

namespace SchedulerAISample
{
	/// <summary>
	/// Palette of colors used to render appointments in the month view,
	/// matching the design shown in the sample image. Each task gets a
	/// stable color from the palette based on its index.
	/// </summary>
	internal static class AppointmentPalette
	{
		private static readonly string[] Hex =
		{
			"#8B5CF6", // violet
			"#B45309", // brown / amber
			"#10B981", // emerald
			"#EF4444", // red
			"#3B82F6", // blue
			"#F97316", // orange
			"#0EA5E9", // sky
			"#EC4899", // pink
			"#22C55E", // green
			"#A855F7"  // purple
		};

		public static Color GetColor(int index)
		{
			var safe = ((index % Hex.Length) + Hex.Length) % Hex.Length;
			return Color.FromArgb(Hex[safe]);
		}
	}
}

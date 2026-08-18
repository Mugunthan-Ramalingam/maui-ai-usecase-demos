using Syncfusion.Maui.Maps;

namespace SmartVehicleCare.Models;

public class CustomMarker : MapMarker
{
    public string? Name              { get; set; }
    public string? Details           { get; set; }
    public string? Address           { get; set; }
    public string? Distance          { get; set; }
    public string? ImageName         { get; set; }
    public Uri?    Image             { get; set; }
    public bool    IsCurrentLocation { get; set; }
}

namespace WeatherApp.Application.Common;

public sealed class LocationNotFoundException() : Exception("The selected location is not in the available catalog.");

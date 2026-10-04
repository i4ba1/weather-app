namespace WeatherApp.Domain;

public static class Temperature
{
    public static double ToCelsius(double fahrenheit) => (fahrenheit - 32) * 5 / 9;
}

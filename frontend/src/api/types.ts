export interface CountryDto { code: string; name: string }
export interface CityDto { name: string; countryCode: string }
export interface WeatherDto {
  city: string;
  countryCode: string;
  observedAtUtc: string;
  windSpeedMetersPerSecond: number;
  windDirectionDegrees: number;
  visibilityMeters: number;
  pressureHpa: number;
  skyConditions: string;
  temperatureFahrenheit: number;
  temperatureCelsius: number;
  dewPointCelsius: number | null;
  dewPointIsCalculated: boolean;
  relativeHumidityPercent: number;
}
export interface CreateNoteCommand { cityName: string; countryCode: string; text: string }
export interface CreatedNoteDto { id: string; createdAtUtc: string }
export interface ProblemDetails { title?: string; detail?: string; errors?: Record<string, string[]> }

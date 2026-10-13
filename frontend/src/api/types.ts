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
export type RefreshCityStatus = 'Pending' | 'Running' | 'Completed' | 'Failed';
export type RefreshJobStatus = RefreshCityStatus | 'CompletedWithErrors';
export interface RefreshCityDto {
  cityName: string; countryCode: string; status: RefreshCityStatus;
  attempts: number; weather: WeatherDto | null; error: string | null;
}
export interface RefreshJobDto {
  id: string; createdAtUtc: string; status: RefreshJobStatus; cities: RefreshCityDto[];
}
export interface AcceptedRefreshJobDto { id: string }
export interface CreateRefreshJobCommand { cities: { cityName: string; countryCode: string }[] }

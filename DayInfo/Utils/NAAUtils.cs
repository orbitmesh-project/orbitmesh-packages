namespace OrbitMesh.DayInfo.Utils;

// NOAA Solar Calculator algorithm (public domain) - Source: https://gml.noaa.gov/grad/solcalc/
// Ported from the original DayInfo package (http://pointofint.blogspot.fr/2014/06/sunrise-and-sunset-in-c.html)
public static class NAAUtils
{
    public static double RadToDeg(double angleRad) => 180.0 * angleRad / Math.PI;

    public static double DegToRad(double angleDeg) => Math.PI * angleDeg / 180.0;

    public static double CalcJD(int year, int month, int day)
    {
        if (month <= 2)
        {
            year -= 1;
            month += 12;
        }
        double a = Math.Floor(year / 100.0);
        double b = 2 - a + Math.Floor(a / 4);
        return Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1)) + day + b - 1524.5;
    }

    public static double CalcJD(DateTime date) => CalcJD(date.Year, date.Month, date.Day);

    public static double CalcTimeJulianCent(double jd) => (jd - 2451545.0) / 36525.0;

    public static double CalcJDFromJulianCent(double t) => t * 36525.0 + 2451545.0;

    public static double CalcGeomMeanLongSun(double t)
    {
        double l0 = 280.46646 + t * (36000.76983 + 0.0003032 * t);
        while (l0 > 360.0) l0 -= 360.0;
        while (l0 < 0.0) l0 += 360.0;
        return l0;
    }

    public static double CalcGeomMeanAnomalySun(double t) => 357.52911 + t * (35999.05029 - 0.0001537 * t);

    public static double CalcEccentricityEarthOrbit(double t) => 0.016708634 - t * (0.000042037 + 0.0000001267 * t);

    public static double CalcSunEqOfCenter(double t)
    {
        double m = CalcGeomMeanAnomalySun(t);
        double mrad = DegToRad(m);
        double sinm = Math.Sin(mrad);
        double sin2m = Math.Sin(mrad + mrad);
        double sin3m = Math.Sin(mrad + mrad + mrad);
        return sinm * (1.914602 - t * (0.004817 + 0.000014 * t)) + sin2m * (0.019993 - 0.000101 * t) + sin3m * 0.000289;
    }

    public static double CalcSunTrueLong(double t) => CalcGeomMeanLongSun(t) + CalcSunEqOfCenter(t);

    public static double CalcSunTrueAnomaly(double t) => CalcGeomMeanAnomalySun(t) + CalcSunEqOfCenter(t);

    public static double CalcSunRadVector(double t)
    {
        double v = CalcSunTrueAnomaly(t);
        double e = CalcEccentricityEarthOrbit(t);
        return 1.000001018 * (1 - e * e) / (1 + e * Math.Cos(DegToRad(v)));
    }

    public static double CalcSunApparentLong(double t)
    {
        double o = CalcSunTrueLong(t);
        double omega = 125.04 - 1934.136 * t;
        return o - 0.00569 - 0.00478 * Math.Sin(DegToRad(omega));
    }

    public static double CalcMeanObliquityOfEcliptic(double t)
    {
        double seconds = 21.448 - t * (46.8150 + t * (0.00059 - t * 0.001813));
        return 23.0 + (26.0 + seconds / 60.0) / 60.0;
    }

    public static double CalcObliquityCorrection(double t)
    {
        double e0 = CalcMeanObliquityOfEcliptic(t);
        double omega = 125.04 - 1934.136 * t;
        return e0 + 0.00256 * Math.Cos(DegToRad(omega));
    }

    public static double CalcSunRtAscension(double t)
    {
        double e = CalcObliquityCorrection(t);
        double lambda = CalcSunApparentLong(t);
        double tananum = Math.Cos(DegToRad(e)) * Math.Sin(DegToRad(lambda));
        double tanadenom = Math.Cos(DegToRad(lambda));
        return RadToDeg(Math.Atan2(tananum, tanadenom));
    }

    public static double CalcSunDeclination(double t)
    {
        double e = CalcObliquityCorrection(t);
        double lambda = CalcSunApparentLong(t);
        double sint = Math.Sin(DegToRad(e)) * Math.Sin(DegToRad(lambda));
        return RadToDeg(Math.Asin(sint));
    }

    public static double CalcEquationOfTime(double t)
    {
        double epsilon = CalcObliquityCorrection(t);
        double l0 = CalcGeomMeanLongSun(t);
        double e = CalcEccentricityEarthOrbit(t);
        double m = CalcGeomMeanAnomalySun(t);

        double y = Math.Tan(DegToRad(epsilon) / 2.0);
        y *= y;

        double sin2l0 = Math.Sin(2.0 * DegToRad(l0));
        double sinm = Math.Sin(DegToRad(m));
        double cos2l0 = Math.Cos(2.0 * DegToRad(l0));
        double sin4l0 = Math.Sin(4.0 * DegToRad(l0));
        double sin2m = Math.Sin(2.0 * DegToRad(m));

        double etime = y * sin2l0 - 2.0 * e * sinm + 4.0 * e * y * sinm * cos2l0
            - 0.5 * y * y * sin4l0 - 1.25 * e * e * sin2m;

        return RadToDeg(etime) * 4.0;
    }

    public static double CalcHourAngleSunrise(double lat, double solarDec)
    {
        double latRad = DegToRad(lat);
        double sdRad = DegToRad(solarDec);
        return Math.Acos(Math.Cos(DegToRad(90.833)) / (Math.Cos(latRad) * Math.Cos(sdRad)) - Math.Tan(latRad) * Math.Tan(sdRad));
    }

    public static double CalcSolNoonUTC(double t, double longitude)
    {
        double tnoon = CalcTimeJulianCent(CalcJDFromJulianCent(t) + longitude / 360.0);
        double eqTime = CalcEquationOfTime(tnoon);
        double solNoonUTC = 720 + longitude * 4 - eqTime;

        double newt = CalcTimeJulianCent(CalcJDFromJulianCent(t) - 0.5 + solNoonUTC / 1440.0);
        eqTime = CalcEquationOfTime(newt);
        return 720 + longitude * 4 - eqTime;
    }

    public static double CalcSunSetUTC(double jd, double latitude, double longitude)
    {
        double t = CalcTimeJulianCent(jd);
        double eqTime = CalcEquationOfTime(t);
        double solarDec = CalcSunDeclination(t);
        double hourAngle = -CalcHourAngleSunrise(latitude, solarDec);
        double delta = longitude + RadToDeg(hourAngle);
        return 720 - 4.0 * delta - eqTime;
    }

    public static double CalcSunRiseUTC(double jd, double latitude, double longitude)
    {
        double t = CalcTimeJulianCent(jd);
        double eqTime = CalcEquationOfTime(t);
        double solarDec = CalcSunDeclination(t);
        double hourAngle = CalcHourAngleSunrise(latitude, solarDec);
        double delta = longitude + RadToDeg(hourAngle);
        return 720 - 4.0 * delta - eqTime;
    }

    public static DateTime? GetDateTime(double time, int timezone, DateTime date, bool dst)
    {
        double timeLocal = time + timezone * 60.0 + (dst ? 60.0 : 0.0);
        return MinutesToDateTime(timeLocal, date);
    }

    private static DateTime? MinutesToDateTime(double minutes, DateTime date)
    {
        if (minutes < 0 || minutes >= 1440)
        {
            return null;
        }
        double floatHour = minutes / 60.0;
        double hour = Math.Floor(floatHour);
        double floatMinute = 60.0 * (floatHour - Math.Floor(floatHour));
        double minute = Math.Floor(floatMinute);
        double floatSec = 60.0 * (floatMinute - Math.Floor(floatMinute));
        double second = Math.Floor(floatSec + 0.5);
        if (second > 59)
        {
            second = 0;
            minute += 1;
        }
        if (second >= 30) minute++;
        if (minute > 59)
        {
            minute = 0;
            hour += 1;
        }
        return new DateTime(date.Year, date.Month, date.Day, (int)hour, (int)minute, (int)second);
    }
}

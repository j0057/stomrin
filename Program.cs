using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

namespace Stomrin
{
    static class Configuration
    {
        public static string WATCH_DIR => Environment.GetEnvironmentVariable("STOMRIN_WATCH_DIR")
            ?? ".";

        public static string SERVICE_URL => Environment.GetEnvironmentVariable("STOMRIN_SERVICE_URL")
            ?? "http://pb-portals.omrin.nl:7980/burgerportal/ServiceKalender.svc";
    }

    static class Extensions
    {
        public static void Save(this IEnumerable<string> lines, string filename, string newline = "\n")
        {
            using (var stream = File.Create(filename))
            {
                using (var writer = new StreamWriter(stream) { NewLine = newline })
                {
                    foreach (var line in lines)
                    {
                        writer.WriteLine(line);
                    }
                }
            }
        }

        public static DateTime SetTime(this DateTime self, int h, int m, int s, int ms)
        {
            return new DateTime(self.Year, self.Month, self.Day, h, m, s, ms, DateTimeKind.Local);
        }

        public static string ToISOString(this DateTime self)
        {
            return string.Format("{0:0000}{1:00}{2:00}T{3:00}{4:00}{5:00}", self.Year, self.Month, self.Day, self.Hour, self.Minute, self.Second);
        }

        public static string iCalendarEscape(this string self)
        {
            return self
                .Replace("\\", "\\\\")
                .Replace(";", "\\;")
                .Replace(",", "\\,")
                .Replace("\n", "\\n");
        }

        public static IEnumerable<string> iCalendarFoldLine(this string self)
        {
            yield return self.Substring(0, Math.Min(75, self.Length));
            for (var i = 75; i < self.Length; i += 74)
            {
                yield return " " + self.Substring(i, Math.Min(i + 74, self.Length)-i);
            }
        }
    }

    class Program
    {
        static string[] MAAND = "Januari Februari Maart April Mei Juni Juli Augustus September Oktober November December".Split(' ');

        static Regex I_CAN_HAS_JOB_PLZ = new Regex(@"^(\d{4})-(\d{4})([a-z]{2})-(\d+)(-([a-z0-9]+))?\.plz$");

        static (Omrin.AansluitingValidatie, Omrin.KalenderObject) GetKalender(string filename, string postcode, int huisnummer, string toevoeging, int jaar)
        {
            var client = new Omrin.Service1Client(Omrin.Service1Client.EndpointConfiguration.CustomBinding_Service1, Configuration.SERVICE_URL) as Omrin.Service1;

            Console.WriteLine($"{filename}: calling ValidateAansluiting {postcode} {huisnummer} {toevoeging}");
            var aansluiting = client.ValidateAansluiting(postcode, huisnummer, toevoeging);

            Console.WriteLine($"{filename}: aansluitingID {aansluiting.AansluitingID} is {aansluiting.Straat} {aansluiting.Huistnummer}{aansluiting.Toevoeging} in {aansluiting.Woonplaats}");

            Console.WriteLine($"{filename}: calling GetKalender {aansluiting.AansluitingID} {jaar}");
            var kalender = client.GetKalender(aansluiting.AansluitingID, jaar);

            Console.WriteLine($"{filename}: {kalender.Omschrijving}");

            if (aansluiting.AansluitingID == -1 || kalender == null || kalender.Groepen == null)
            {
                throw new ApplicationException($"{postcode} {huisnummer}{toevoeging} niet gevonden");
            }

            return (aansluiting, kalender);
        }

        static XElement CreateHTMLCalendar(Omrin.KalenderGroepObject kalenderGroep)
        {
            var x = (XNamespace)"http://www.w3.org/1999/xhtml";
            return new XElement(x + "div",
                new XElement(x + "h2", kalenderGroep.Omschrijvging),
                new XElement(x + "p", kalenderGroep.Info),
                kalenderGroep.Datums == null
                ? new XElement(x + "p", kalenderGroep.info2)
                : new XElement(x + "table",
                    kalenderGroep.Datums
                        .GroupBy(kgd => kgd.Datum.Month)
                        .OrderBy(g => g.Key)
                        .Select(g => new XElement(x + "tr",
                            new XElement(x + "th", MAAND[g.Key-1]),
                            new XElement(x + "td", string.Join(", ", g.OrderBy(d => d.Datum.Day).Select(d => d.Datum.Day))))))) ;
        }

        static XElement CreateHTML(int jaar, Omrin.AansluitingValidatie aansluiting, Omrin.KalenderObject kalender, string icalFilename)
        {
            var x = (XNamespace)"http://www.w3.org/1999/xhtml";
            return new XElement(x + "html",
                new XElement(x + "head",
                    new XElement(x + "title", $"Afvalkalender {jaar}"),
                    new XElement(x + "meta", new XAttribute("name", "viewport"), new XAttribute("content", "width=device-width, initial-scale=1")),
                    new XElement(x + "style",
                        ".flex { display:flex; flex-wrap:wrap; }",
                        ".flex>div { max-width: 20em; margin-right:5px; }"
                    )),
                new XElement(x + "body",
                    new XElement(x + "h1", $"Afvalkalender {jaar}"),
                    new XElement(x + "p", $"{aansluiting.Straat} {aansluiting.Huistnummer}{aansluiting.Toevoeging}, {aansluiting.Postcode} {aansluiting.Woonplaats}"),
                    new XElement(x + "p",
                        "Link naar iCal bestand om toe te voegen in Google/Outlook/iCloud/Android: ",
                        new XElement(x + "a", new XAttribute("href", icalFilename), icalFilename)),
                    new XElement(x + "div", new XAttribute("class", "flex"),
                        kalender.Groepen.Select(CreateHTMLCalendar))));
        }

        static IEnumerable<string> CreateICS(int jaar, Omrin.AansluitingValidatie aansluiting, Omrin.KalenderObject kalender)
        {
            /* BEGIN:VEVENT
             * DTSTART:20150207T210500Z
             * DTEND:20150207T211000Z
             * DTSTAMP:20150204T133100Z
             * UID:testcal-2@stomrin.nl
             * CREATED:20150129T204608Z
             * DESCRIPTION:Test event 2 description
             * LOCATION:Zaailand 16\, Leeuwarden
             * SEQUENCE:1
             * SUMMARY:Test event 2
             * END:VEVENT
             */

            var sequence = 0;
            var adres = $"{aansluiting.Straat} {aansluiting.Huistnummer}{aansluiting.Toevoeging}, {aansluiting.Woonplaats}".iCalendarEscape();
            var utcnow = $"{DateTime.UtcNow.ToISOString()}Z";

            yield return $"BEGIN:VCALENDAR";
            yield return $"VERSION:2.0";
            yield return $"X-WR-CALNAME:Afvalkalender {jaar}";
            yield return $"X-WR-CALDESC:{adres}";
            yield return $"CALSCALE:GREGORIAN";

            foreach (var groep in kalender.Groepen)
            {
                var datums = groep.Datums ?? Enumerable.Empty<Omrin.KalenderGroepDatum>();

                // XXX: i used to sort events by date, does it really matter?
                foreach (var datum in datums)
                {
                    // XXX: these codes lead to churn ... better to detect times in description field?
                    var time = groep.Afbeelding == "SOR" ? datum.Datum.SetTime(7, 30, 0, 0)
                             : groep.Afbeelding == "BIO" ? datum.Datum.SetTime(7, 30, 0, 0)
                             : groep.Afbeelding == "EGF" ? datum.Datum.SetTime(7, 30, 0, 0)
                             : groep.Afbeelding == "PAP" ? datum.Datum.SetTime(17, 30, 0, 0)
                             : datum.Datum;

                    yield return $"BEGIN:VEVENT";
                    yield return $"SEQUENCE:{++sequence}";
                    yield return $"UID:{aansluiting.AansluitingID}-{jaar}-{sequence}@stomrin.nl";
                    yield return $"DTSTART;TZID=Europe/Amsterdam:{time.ToISOString()}";
                    yield return $"DTEND;TZID=Europe/Amsterdam:{time.AddHours(1).ToISOString()}";
                    yield return $"DTSTAMP:{utcnow}";
                    yield return $"SUMMARY:{groep.Omschrijvging.iCalendarEscape()}";
                    yield return $"DESCRIPTION:{groep.Info.Replace("\r", "").Replace("\n", "").iCalendarEscape()}";
                    yield return $"END:VEVENT";
                }
            }
            yield return $"END:VCALENDAR";
        }

        static void HandleJob(string filename, int jaar, string postcode, int huisnr, string toevoeging)
        {
            Console.WriteLine($"{filename}: starting");

            var acceptFilename = Path.ChangeExtension(filename, "acc");
            var errorFilename = Path.ChangeExtension(filename, "err");
            var icalFilename = Path.ChangeExtension(filename, "ics");
            var htmlFilename = Path.ChangeExtension(filename, "html");

            try
            {
                File.Create(acceptFilename).Dispose();
                File.Delete(filename);

                var (aansluiting, kalender) = GetKalender(filename, postcode, huisnr, toevoeging, jaar);

                Console.WriteLine($"{filename}: saving {icalFilename}");
                CreateICS(jaar, aansluiting, kalender)
                    .SelectMany(line => line.iCalendarFoldLine())
                    .Save(icalFilename, newline: "\r\n");

                Console.WriteLine($"{filename}: saving {htmlFilename}");
                CreateHTML(jaar, aansluiting, kalender, Path.GetFileName(icalFilename))
                    .Save(htmlFilename);
            }
            catch (ApplicationException e)
            {
                Console.Error.WriteLine($"{filename}: application error -- {e.Message}");

                new[] { e.Message }.Save(errorFilename);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"{filename}: {e.GetType().FullName}: {e.Message}");
                Console.Error.WriteLine(e.StackTrace);

                new[] { "Server error" }.Save(errorFilename);
            }
            finally
            {
                File.Delete(acceptFilename);
            }
            Console.WriteLine($"{filename}: done");
        }

        static FileSystemWatcher CreateWatcher()
        {
            var watcher = new FileSystemWatcher(Configuration.WATCH_DIR);

            watcher.Created += (s, e) =>
            {
                var filename = Path.GetFileName(e.FullPath);
                var match = I_CAN_HAS_JOB_PLZ.Match(filename);
                if (match.Success)
                {
                    HandleJob(
                        filename: e.FullPath,
                        jaar: int.Parse(match.Groups[1].Value),
                        postcode: $"{match.Groups[2].Value} {match.Groups[3].Value.ToUpper()}",
                        huisnr: int.Parse(match.Groups[4].Value),
                        toevoeging: match.Groups[6].Value);
                }
            };

            watcher.EnableRaisingEvents = true;

            return watcher;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Stomrin worker starting");
            Console.WriteLine($"Watch dir: {Configuration.WATCH_DIR}");
            Console.WriteLine($"Service URL: {Configuration.SERVICE_URL}");
            Console.WriteLine($"Current directory: {Environment.CurrentDirectory}");

            using (var watcher = CreateWatcher())
            {
                Thread.Sleep(Timeout.Infinite);
            }
        }
    }
}

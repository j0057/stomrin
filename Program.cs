using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Stomrin
{
    static class Configuration
    {
        public static string WATCH_DIR => Environment.GetEnvironmentVariable("STOMRIN_WATCH_DIR");

        public static string SERVICE_URL => Environment.GetEnvironmentVariable("STOMRIN_SERVICE_URL");
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

        public static IEnumerable<T> AddItems<T>(this IEnumerable<T> self, params T[] items)
        {
            return self.Concat(items);
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

        static void GetKalender(string postcode, int huisnummer, string toevoeging, int jaar, out Omrin.AansluitingValidatie aansluiting, out Omrin.KalenderObject kalender)
        {
            var client = new Omrin.Service1Client(Omrin.Service1Client.EndpointConfiguration.CustomBinding_Service1, Configuration.SERVICE_URL) as Omrin.Service1;

            Console.WriteLine("ValidateAansluiting {0},{1},{2}", postcode, huisnummer, toevoeging);
            aansluiting = client.ValidateAansluiting(postcode, huisnummer, toevoeging);

            Console.WriteLine("GetKalender {0},{1}", aansluiting.AansluitingID, jaar);
            kalender = client.GetKalender(aansluiting.AansluitingID, jaar);
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

        static XElement CreateHTML(int jaar, Omrin.AansluitingValidatie aansluiting, Omrin.KalenderObject kalender)
        {
            var x = (XNamespace)"http://www.w3.org/1999/xhtml";
            var title = string.Format("Afvalkalender {0}", jaar);
            var adres = string.Format("{0} {1}{2}, {3}", aansluiting.Straat, aansluiting.Huistnummer, aansluiting.Toevoeging, aansluiting.Woonplaats);
            return new XElement(x + "html",
                new XElement(x + "head",
                    new XElement(x + "title", title),
                    new XElement(x + "meta", new XAttribute("name", "viewport"), new XAttribute("content", "width=device-width, initial-scale=1"))),
                new XElement(x + "body",
                    new XElement(x + "h1", title),
                    new XElement(x + "p", adres),
                    kalender.Groepen.Select(CreateHTMLCalendar)));
        }

        static IEnumerable<string> CreateCalendarEvent(int sequence, string summary, string description, string location, DateTime start, DateTime end)
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
            return Enumerable.Empty<string>()
                .AddItems(
                    "BEGIN:VEVENT",
                    string.Format("SEQUENCE:{0}", sequence),
                    string.Format("UID:{0}-{1}-{2}@stomrin.nl", location.ToLower().Replace(" ", "-").Replace(",", ""), start.Year, sequence),
                    string.Format("DTSTART;TZID=Europe/Amsterdam:{0}", start.ToISOString()),
                    string.Format("DTEND;TZID=Europe/Amsterdam:{0}", end.ToISOString()),
                    string.Format("DTSTAMP;TZID=Europe/Amsterdam:{0}", start.ToISOString()))
                .Concat(("SUMMARY:" + summary.iCalendarEscape()).iCalendarFoldLine())
                .Concat(("DESCRIPTION:" + description.iCalendarEscape()).iCalendarFoldLine())
                .AddItems("END:VEVENT");
        }

        static IEnumerable<string> CreateCalendar(int jaar, Omrin.AansluitingValidatie aansluiting, Omrin.KalenderObject kalender)
        {
            var codes = "GCP GFT PAP KCA GRF".Split();
            var adres = string.Format("{0} {1}{2}, {3}", aansluiting.Straat, aansluiting.Huistnummer, aansluiting.Toevoeging, aansluiting.Woonplaats);
            return Enumerable.Empty<string>()
                .AddItems(
                    "BEGIN:VCALENDAR",
                    "VERSION:2.0")
                .Concat(string.Format("X-WR-CALNAME:Afvalkalender {0}", jaar).iCalendarFoldLine())
                .Concat(string.Format("X-WR-CALDESC:{0}", adres.iCalendarEscape()).iCalendarFoldLine())
                .AddItems("CALSCALE:GREGORIAN")
                .Concat(kalender.Groepen
                    .Where(kg => codes.Contains(kg.Afbeelding))
                    .SelectMany(
                        kg => kg.Datums,
                        (kg, kgd) => Tuple.Create(
                            kg,
                              kg.Afbeelding == "GCP" ? kgd.Datum.SetTime(7, 30, 0, 0)
                            : kg.Afbeelding == "GFT" ? kgd.Datum.SetTime(7, 30, 0, 0)
                            : kg.Afbeelding == "PAP" ? kgd.Datum.SetTime(17, 30, 0, 0)
                            : kgd.Datum))
                    .OrderBy(e => e.Item2)
                    .SelectMany((e,i) => CreateCalendarEvent(i, e.Item1.Omschrijvging, e.Item1.Info, adres, e.Item2, e.Item2.AddHours(1))))
                .AddItems("END:VCALENDAR");
        }

        static void HandleJob(string filename, int jaar, string postcode, int huisnr, string toevoeging)
        {
            Console.WriteLine("Job start: {0}", filename);
            try
            {
                File.Create(Path.ChangeExtension(filename, "acc")).Dispose();
                File.Delete(filename);

                Omrin.AansluitingValidatie aansluiting;
                Omrin.KalenderObject kalender;

                GetKalender(postcode, huisnr, toevoeging, jaar, out aansluiting, out kalender);

                if (aansluiting.AansluitingID == -1 || kalender == null || kalender.Groepen == null)
                {
                    throw new ApplicationException(string.Format("{0} {1}{2} niet gevonden", postcode, huisnr, toevoeging));
                }

                var icalFilename = Path.ChangeExtension(filename, "ics");
                Console.WriteLine("Saving {0}", icalFilename);
                CreateCalendar(jaar, aansluiting, kalender).Save(icalFilename, newline: "\r\n");

                var htmlFilename = Path.ChangeExtension(filename, "html");
                Console.WriteLine("Saving {0}", htmlFilename);
                CreateHTML(jaar, aansluiting, kalender).Save(htmlFilename);
            }
            catch (ApplicationException e)
            {
                new List<string>() { e.Message }.Save(Path.ChangeExtension(filename, "err"));
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(string.Format("{0}: {1}", e.GetType().FullName, e.Message));
                Console.Error.WriteLine(e.StackTrace);

                new List<string>() { "Server error" }.Save(Path.ChangeExtension(filename, "err"));
            }
            finally
            {
                File.Delete(Path.ChangeExtension(filename, "acc"));
            }
            Console.WriteLine("Job end: {0}", filename);
        }

        static void WatchDirectory()
        {
            while (true)
            {
                Directory.GetFiles(Configuration.WATCH_DIR)
                    .Select(filename => Tuple.Create(filename, I_CAN_HAS_JOB_PLZ.Match(Path.GetFileName(filename))))
                    .Where(t => t.Item2.Success)
                    .ToList()
                    .ForEach(t => HandleJob(
                        filename: t.Item1,
                        jaar: int.Parse(t.Item2.Groups[1].Value),
                        postcode: t.Item2.Groups[2].Value + " " + t.Item2.Groups[3].Value.ToUpper(),
                        huisnr: int.Parse(t.Item2.Groups[4].Value),
                        toevoeging: t.Item2.Groups[6].Value));
                Thread.Sleep(100);
            }
        }

        static void Main(string[] args)
        {
            WatchDirectory();
        }
    }
}

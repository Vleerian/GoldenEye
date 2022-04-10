using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Xml;
using System.Xml.Serialization;
using System.Text.RegularExpressions;

using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp<GoldenEye>();
return app.Run(args);

internal sealed class GoldenEye : AsyncCommand<GoldenEye.Settings>
{
    const int PollSpeed = 750;
    const string VersionNumber = "2.1";

    HttpClient client = new();

    const string Check = "[green]✓[/]";
    const string Cross = "[red]x[/]";
    const string Arrow = "→";

    public async override Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        client.DefaultRequestHeaders.Add("User-Agent", $"GoldenEye/{VersionNumber} User/{settings.MainNation} (By 20XX, Atagait@hotmail.com)");

        string target = CleanName(settings.Region!);

        if(!Directory.Exists("./data"))
            Directory.CreateDirectory("./data");

        RegionAPI Region_Dump = default!;
        if(!settings.NoCompare)
        {
            if(!File.Exists(settings.DataDump))
            {
                Logger.Error("Specified data dump file does not exist.");
                return 0;
            }

            string XML = "";
            FileInfo dumpFile = new FileInfo(settings.DataDump);
            if(settings.DataDump.EndsWith(".gz"))
            {
                Logger.Processing("Unzipping data dump");
                XML = UnzipDump(dumpFile.FullName);
            }
            else
            {
                Logger.Processing("Loading data dump");
                XML = File.ReadAllText(dumpFile.FullName);
            }

            var RData = BetterDeserialize<RegionDataDump>(XML);
            Region_Dump = RData.Regions.Where(R => CleanName(R.name)== target).FirstOrDefault()!;
            if(Region_Dump == null || Region_Dump == default)
            {
                Logger.Warning($"region {target} not present in data dump.");
                AnsiConsole.MarkupLine("Run GoldenEye with --no-compare to exclude data dump comparison.");
                return 0;
            }
        }

        Logger.Request("Fetching region data from the API.");
        var Region_Req = MakeReq($"https://www.nationstates.net/cgi-bin/api.cgi?region={target}");
        if(Region_Req.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Logger.Warning($"Region could not be found.");
            return 0;
        }
        RegionAPI Region_Current = BetterDeserialize<RegionAPI>(await Region_Req.Content.ReadAsStringAsync());
        
        StringBuilder Output = new();
        Output.AppendLine($"Report on [yellow]{Region_Current.name}[/]");
        Output.AppendLine($"Raidable: {(Region_Current.DelegateAuth.Contains("X") ? Check : Cross)}");
        Output.AppendLine($"Founderless: {(!Region_Current.nations.Contains(Region_Current.Founder) ? Check : Cross)}");

        // If on compare, compare the region's current population to the dump, otherwise append to the output.
        Logger.Processing("Enumerating nations");
        if(!settings.NoCompare)
        {
            int NationChange = Region_Current.NumNations - Region_Dump!.NumNations;
            Output.AppendLine($"Nations: {Region_Dump.NumNations} [{RWG(NationChange)}]{Arrow}[/] {Region_Current.NumNations} (Net {NationChange})");
        }
        else
            Output.AppendLine($"Nations: {Region_Current.NumNations}");
        
        // If on compare, compare the region's current embassy numbers to the dump
        Logger.Processing("Enumerating Embassies");
        if(!settings.NoCompare)
        {
            int EmbassyChange = Region_Current.Embassies.Count() - Region_Dump.Embassies.Count();
            Output.AppendLine($"Embassies: {Region_Dump.Embassies.Count()} [{RWG(EmbassyChange)}]{Arrow}[/] {Region_Current.Embassies.Count()} (Net {EmbassyChange})");
        }
        else
            Output.AppendLine($"Embassies:{Region_Current.Embassies.Count()}");
        
        // If a CDS scan was requested, fetch the CDS and compare the embassies to it
        if(settings.CDS_Scan)
        {
            Logger.Processing("Performing CDS scan");
            // The filename used to store the CDS XML
            string CDS_Filename = "./data/CDS_"+DateTime.Now.ToString("dd.MM.yyyy")+".xml";
            string CDS_XML = "";

            // If the CDS has already been downloaded, use it, otherwise download it
            if(File.Exists(CDS_Filename))
            {
                CDS_XML = File.ReadAllText(CDS_Filename);
            }
            else
            {
                Logger.Request("Fetching the CDS dispatch");
                var CDS_Req = MakeReq($"https://www.nationstates.net/cgi-bin/api.cgi?q=dispatch;dispatchid=1081644");
                CDS_XML = await CDS_Req.Content.ReadAsStringAsync();
                File.WriteAllText(CDS_Filename, CDS_XML);
            }

            // Deserialize and parse the CDS to get the region names
            var CDS_Dis = BetterDeserialize<WorldAPI>(CDS_XML);
            string CDS_Table = CDS_Dis.Dispatch.Text.Split("table")[1];
            var M = Regex.Matches(CDS_Table, @"\[region\]([^\]]*)\[\/region\]");
            string[] facist_regions = M.Select(M=>CleanName(M.Groups[1].Value)).ToArray();

            // Pick out the fash embassies
            string[] Bad_Embassies = Region_Current.Embassies.Where(E=>facist_regions.Contains(CleanName(E))).ToArray();
            int CDS_Count = Bad_Embassies.Count();

            // Do the comparisons and/or append to output
            if(!settings.NoCompare)
            {
                int Last_CDS_Count = Region_Dump.Embassies.Count(R=>facist_regions.Contains(CleanName(R)));
                int NetCDS = Last_CDS_Count - CDS_Count;
                Output.AppendLine($"CDS Embassies: {Last_CDS_Count} [{RWG(NetCDS)}]{Arrow}[/] {CDS_Count} (Net {NetCDS})");
            }
            else
                Output.AppendLine($"CDS Embassies: {CDS_Count}");
            if(CDS_Count > 0)
                Output.AppendLine($"Bad Embassies: {string.Join(" ", Bad_Embassies)}");
        }

        Logger.Processing("Enumerating ROs");
        // Do the  comparisons on ROs
        int Officer_Count = Region_Current.Officers.Count(O=>O.Nation?.ToLower() != "cte");
        if(!settings.NoCompare)
        {
            int Off_Old = Region_Dump.Officers.Count(O=>O.Nation?.ToLower() != "cte");
            int Off_Net = Officer_Count - Off_Old;
            Output.AppendLine($"Officers: {Off_Old} [{RWG(Off_Net)}]{Arrow}[/] {Officer_Count} (Net {Off_Net})");
        }
        else
            Output.AppendLine($"Officers:{Officer_Count}");

        // Calculate the threshholds for invisible vs visible passwords
        int Vis = Region_Current.NumNations*20;
        int Invis = Region_Current.NumNations*40;

        List<NationAPI> Nations = new();

        // Do a scan of the delegate
        NationAPI regionDelegate = null;
        Logger.Processing("Processing Delegate");
        if ( Region_Current.Delegate != null && Region_Current.Delegate.Trim() != string.Empty)
        {
            regionDelegate = await GetNation(Region_Current.Delegate);
            Nations.Add(regionDelegate);
            Output.AppendLine(await GenNationReport(regionDelegate, Vis, Invis, Region_Current.DelegateAuth));
        }

        Logger.Processing("Processing ROs");
        foreach(var Officer in Region_Current.Officers.Where(O=>O.Nation?.ToLower() != "cte"))
        {
            var regionOfficer = await GetNation(Officer.Nation);
            Nations.Add(regionOfficer);
            Output.AppendLine(await GenNationReport(regionOfficer, Vis, Invis, Officer.OfficerAuth));
        }

        // Get the rest of the nations
        if(settings.FullReport)
        {
            Logger.Processing("Generating full report");

            // This will be the group of Delegates + RO
            CSVDump("DelsRO.csv", Nations);
            Logger.Info("Delegates and ROs report done.");
            
            try {
                // This is used to check if nations have endorsed the ROs
                string[] RO_Endos = Nations
                    .SelectMany(N=>N.Endorsements) // Round up all the endorsements on RO and Delegates
                    .Where(x => x.Trim() != string.Empty) // Remove empties
                    .GroupBy(x => x) // Duplicate removal step one
                    .Select(x=>x.First()) // duplicate removal step two
                    .ToArray(); // Convert it to an array

                // Select only nations that haven't already been scanned
                string[] scanned = Nations.Select(N=>CleanName(N.name)).ToArray();
                var Unscanned = Region_Current.Nations.Where(N => !scanned.Contains(CleanName(N)));
                foreach(var Nation in Unscanned)
                {
                    var tmp = await GetNation(Nation);
                    tmp.EndorsingDel = regionDelegate?.Endorsements.Contains(Nation) ?? false;
                    tmp.EndorsingRO = RO_Endos.Contains(Nation);
                    Nations.Add(tmp);
                }
            } catch (Exception e)
            {
                Logger.Error("Exception", e);
            }

            // Nations endorsing the delegate
            CSVDump("DelEndos.csv", Nations.Where(N => N.EndorsingDel));
            // Create a group of nations endorsing the ROs
            CSVDump("ROEndos.csv", Nations.Where(N => N.EndorsingRO));
            // Create a group of all the Non-WA nations
            CSVDump("NonWA.csv", Nations.Where(N=>!N.IsWA));

            if(settings.DefenderPoints != null)
            {
                // Set the points up into an array
                string[] DefenderPoints = new string[] { settings.DefenderPoints.Trim() };
                if(settings.DefenderPoints.Contains(","))
                    DefenderPoints = settings.DefenderPoints.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                // Sanitize the point names
                DefenderPoints = DefenderPoints.Select(P=>CleanName(P)).ToArray();
                var Points = Nations.Where(N=>DefenderPoints.Contains(CleanName(N.name)));
                // Defender point nations
                CSVDump("DefenderPoints.csv", Points);
                // Nations endorsing the specified defender point nations
                CSVDump("DefenderEndos.csv", Nations.Where(N=>Points.Any(P=>P.Endorsements.Contains(CleanName(N.name)))));
            }
        }

        AnsiConsole.MarkupLine(Output.ToString());

        return 0;
    }

    public static string CleanName(string name) => name.ToLower().Replace(' ', '_');

    static async void CSVDump(string Filename, IEnumerable<NationAPI> nations)
    {
        StringBuilder output = new();
        output.AppendLine($"name,Endos,IsWA,InfluenceLevel,SPDR,Residency");
        foreach(var nation in nations)
        {
            output.AppendLine(nation.GetCSV());
        }
        File.WriteAllText(Filename, output.ToString());
    }

    async Task<NationAPI> GetNation(string nation)
    {
        string NationFile = $"./data/{nation}_{DateTime.Now.ToString("dd.MM.yyyy")}.xml";
        string XML = "";
        if(!File.Exists(NationFile))
        {
            Logger.Request($"Fetching data for {nation}");
            var Nation_Req = MakeReq($"https://www.nationstates.net/cgi-bin/api.cgi?nation={nation};q=name+endorsements+influence+wa+census;scale=65+80");
            if(Nation_Req.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Logger.Warning($"No nation data is available for {nation}");
                return default;
            }
            XML = await Nation_Req.Content.ReadAsStringAsync();
            File.WriteAllText(NationFile, XML);
        }
        else
            XML = File.ReadAllText(NationFile);

        return BetterDeserialize<NationAPI>(XML);
    }

    async Task<string> GenNationReport(NationAPI Nation, int VisThresh, int InvisThresh, string Auth)
    {
        int influence = (int)Nation.CensusData[CensusScore.Influence].CensusScore;

        bool BC = Auth.Contains("B");
        bool Vis = influence >= VisThresh;
        bool Invis = influence >= InvisThresh;

        return $"{Nation.InfluenceLevel.ToUpper()} {Nation.name} - BC: {(BC?Check:Cross)} - WA: {(Nation.IsWA?Check:Cross)} - Endos: {Nation.Endos} - Influence: {influence} - PW:{(Vis?Check:Cross)}{(Invis?Check:Cross)}";
    }

    /// <summary>
    /// Unzips nation.xml.gz and region.xml.gz files
    /// </summary>
    /// <param name="Filename">the .xml.gz file to unzip</param>
    /// <returns></returns>
    private static string UnzipDump(string Filename)
        {
            using (var fileStream = new FileStream(Filename, FileMode.Open))
            {
                using (var gzStream = new GZipStream(fileStream, CompressionMode.Decompress))
                {
                    using (var outputStream = new MemoryStream())
                    {
                        gzStream.CopyTo(outputStream);
                        byte[] outputBytes = outputStream.ToArray(); // No data. Sad panda. :'(
                        return Encoding.Default.GetString(outputBytes);
                    }
                }
            }
        }

    string RWG(int a) => a > 0 ? "green" : a == 0 ? "blue" : "red";

    /// <summary>
    /// This method makes deserializing XML less painful
    /// <param name="url">The URL to request from</param>
    /// <returns>The parsed return from the request.</returns>
    /// </summary>
    T BetterDeserialize<T>(string XML) =>
        (T)new XmlSerializer(typeof(T))!.Deserialize(new StringReader(XML))!;

    /// <summary>
    /// This method waits the delay set by the program, then makes a request
    /// <param name="url">The URL to request from</param>
    /// <returns>The return from the request.</returns>
    /// </summary>
    HttpResponseMessage MakeReq(string url) {
        System.Threading.Thread.Sleep(PollSpeed);
        return client.GetAsync(url).GetAwaiter().GetResult();
    }

    public sealed class Settings : CommandSettings
    {
        [Description("Your nation name for identifying the user to NSAdmin")]
        [CommandArgument(0, "[MainNation]")]
        public string? MainNation { get; init; }

        [Description("Region to Scan")]
        [CommandArgument(1, "[region]")]
        public string? Region { get; init; }

        [Description("Region Data Dump XML file to use")]
        [CommandArgument(2, "[dataDump]")]
        public string? DataDump { get; init; }

        [CommandOption("--cds")]
        [Description("Scan the region's embassies using Civil Defense Siren")]
        [DefaultValue(false)]
        public bool CDS_Scan { get; init; }

        [CommandOption("--no-compare")]
        [Description("Do not compare the region to the data dumps.")]
        [DefaultValue(false)]
        public bool NoCompare { get; init; }

        [CommandOption("--full-report")]
        [Description("Scan all of a region's nations")]
        [DefaultValue(false)]
        public bool FullReport { get; init; }

        [CommandOption("--defender-points")]
        [Description("Comma separated list of defender point nations")]
        public string? DefenderPoints { get; init; }
    }
}

[XmlRoot("REGIONS")]
public class RegionDataDump
{
    [XmlElement("REGION", typeof(RegionAPI))]
    public RegionAPI[] Regions { get; init; }
}

[Serializable(), XmlRoot("REGION")]
public class Officer
{
    [XmlElement("NATION")]
    public string Nation { get; init; }
    [XmlElement("OFFICE")]
    public string Office { get; init; }
    [XmlElement("AUTHORITY")]
    public string OfficerAuth { get; init; }
    [XmlElement("TIME")]
    public int AssingedTimestamp { get; init; }
    [XmlElement("BY")]
    public string AssignedBy { get; init; }
    [XmlElement("ORDER")]
    public int Order { get; init; }
}

[Serializable, XmlRoot("NATION")]
public class NationAPI
{
    public string GetCSV() => $"{name},{Endos},{IsWA},{InfluenceLevel},{SPDR},{Residency}";

    [XmlAttribute("id")]
    public string id { get; init; }

    [XmlElement("NAME")]
    public string? name { get; init; }

    [XmlElement("ENDORSEMENTS")]
    public string? endorsements { get; init; }

    [XmlIgnore]
    public string[] Endorsements => endorsements?.Split(",") ?? new string[0];

    [XmlIgnore]
    public int Endos => Endorsements.Count();

    [XmlElement("UNSTATUS")]
    public string unstatus { get; init; }

    [XmlElement("INFLUENCE")]
    public string InfluenceLevel { get; init; }

    [XmlIgnore]
    public int SPDR => (int)CensusData[CensusScore.Influence].CensusScore;

    [XmlIgnore]
    public int Residency => (int)CensusData[CensusScore.Residency].CensusScore;

    [XmlIgnore]
    public bool IsWA => unstatus.StartsWith("WA");

    [XmlIgnore]
    public bool EndorsingDel;
    [XmlIgnore]
    public bool EndorsingRO;

    [XmlElement("TYPE")]
    public string type { get; init; }

    [XmlElement("FULLNAME")]
    public string fullname { get; init; }

    [XmlElement("FLAG")]
    public string flag { get; init; }

    [XmlArray("CENSUS"), XmlArrayItem("SCALE", typeof(CensusAPI))]
    public List<CensusAPI> CensusScores { get; init; }

    [XmlIgnore]
    public Dictionary<CensusScore, CensusAPI> CensusData =>
        CensusScores.ToDictionary(C => C.Census);
}

[Serializable]
public class CensusAPI
{
    [XmlAttribute("id")]
    public int CensusID { get; init; }

    [XmlIgnore]
    public CensusScore Census => (CensusScore) CensusID;
    
    [XmlElement("SCORE")]
    public float CensusScore  { get; init; }

    [XmlElement("RANK")]
    public float WorldRank { get; init; }

    [XmlElement("RRANK")]
    public float RegionRank { get; init; }

}

[XmlRoot("WORLD")]
public class WorldAPI
{
    [XmlArray("HAPPENINGS")]
    [XmlArrayItem("EVENT", typeof(WorldEvent))]
    public WorldEvent[] Happenings { get; init; }

    [XmlElement("REGIONS")]
    public string Regions { get; init; }

    [XmlElement("FEATUREDREGION")]
    public string Featured { get; init; }

    [XmlElement("NATIONS")]
    public string Nations { get; init; }

    [XmlElement("NEWNATIONS")]
    public string NewNations { get; init; }

    [XmlElement("NUMNATIONS")]
    public int NumNations { get; init; }

    [XmlElement("NUMREGIONS")]
    public int NumRegions { get; init; }

    [XmlElement("DISPATCH")]
    public DispatchAPI Dispatch { get; init; }
}

[Serializable()]
public class WorldEvent
{
    [XmlElement("TIMESTAMP")]
    public long Timestamp { get; init; }
    [XmlElement("TEXT")]
    public string Text { get; init; }
}

[Serializable()]
public class DispatchAPI
{
    [XmlAttribute("id")]
    public int DispatchID { get; init; }
    [XmlElement("TITLE")]
    public string Title { get; init; }
    [XmlElement("AUTHOR")]
    public string Author { get; init; }
    [XmlElement("CATEGORY")]
    public string Category { get; init; }
    [XmlElement("CREATED")]
    public long Created { get; init; }
    [XmlElement("EDITED")]
    public long Edited { get; init; }
    [XmlElement("VIEWS")]
    public int views { get; init; }
    [XmlElement("SCORE")]
    public int score { get; init; }
    [XmlElement("TEXT")]
    public string Text { get; init; }
}

[Serializable(), XmlRoot("REGION")]
public class RegionAPI
{
    //These are values parsed from the data dump
    [XmlElement("NAME")]
    public string name { get; init; }
    public string Name
    {
        get
        {
            return GoldenEye.CleanName(name);
        }
    }

    [XmlElement("NUMNATIONS")]
    public int NumNations { get; init; }
    [XmlElement("NATIONS")]
    public string nations { get; init; }
    [XmlElement("DELEGATE")]
    public string Delegate { get; init; }
    [XmlElement("DELEGATEVOTES")]
    public int DelegateVotes { get; init; }
    [XmlElement("DELEGATEAUTH")]
    public string DelegateAuth { get; init; }
    [XmlElement("FOUNDER")]
    public string Founder { get; init; }
    [XmlElement("FOUNDERAUTH")]
    public string FounderAuth { get; init; }
    [XmlElement("FACTBOOK")]
    public string Factbook { get; init; }
    [XmlArray("OFFICERS"), XmlArrayItem("OFFICER", typeof(Officer))]
    public Officer[] Officers { get; init; }
    [XmlArray("EMBASSIES"), XmlArrayItem("EMBASSY", typeof(string))]
    public string[] Embassies { get; init; }
    [XmlElement("LASTUPDATE")]
    public double lastUpdate { get; init; }
    public double LastUpdate
    {
            get
            {
                //Subtract 4 hours from LastUpdate
                //Seconds into the update is more useful than the UTC update time
                return lastUpdate - (4 *3600);
            }
    }

    //These are values added after the fact by ARCore
    public string[] Nations
    {
        get {
            return nations
                .Replace(' ','_')
                .ToLower()
                .Split(":", StringSplitOptions.RemoveEmptyEntries);
        }
    }

    public long Index;
    public bool hasPassword;
    public bool hasFounder;
    public string FirstNation;
}

public enum CensusScore
{
    CivilRights = 0,
    Economy = 1,
    PoliticalFreedoms = 2,
    Population = 3,
    WealthGaps = 4,
    DeathRate = 5,
    compassion = 6,
    EcoFriendliness = 7,
    SocialConservatism = 8,
    Nudity = 9,
    AutomobileManufacturing = 10,
    CheeseExports = 11,
    BasketWeaving = 12,
    InformtionTechnology = 13,
    PizzaDelivery = 14,
    TroutFishing = 15,
    ArmsManufacturing = 16,
    Agriculture = 17,
    BeverageSales = 18,
    TimberWoodchipping = 19,
    Mining = 20,
    Insurance = 21,
    FurnitureRestoration = 22,
    Retail = 23,
    BookPublishing = 24,
    Gambling = 25,
    Manufacturing = 26,
    GovernmentSize = 27,
    Welfare = 28,
    PublicHealthcare = 29,
    LawEnforcement = 30,
    BusinessSubsidization = 31,
    Religiousness = 32,
    IncomeEquality = 33,
    Niceness = 34,
    Rudeness = 35,
    Intelligence = 36,
    Ignorance = 37,
    PoliticalApathy = 38,
    Health = 39,
    Cheerfulness = 40,
    Weather = 41,
    Compliance = 42,
    Safety = 43,
    Lifespan = 44,
    IdeologicalRadicality = 45,
    DefenseForces = 46,
    Pacifism = 47,
    EconomicFreedom = 48,
    Taxation = 49,
    FreedomFromTaxation = 50,
    Corruption = 51,
    Integrity = 52,
    Authoritarianism = 53,
    YouthRebelliousness = 54,
    Culture = 55,
    Employment = 56,
    PublicTransport = 57,
    Tourism = 58,
    Weaponization = 59,
    RecreationalDrugUse = 60,
    Obesity = 61,
    Secularism = 62,
    EnvironmentalBeauty = 63,
    Charmlessness = 64,
    Influence = 65,
    WorldAssemblyEndorsements = 66,
    Averageness = 67,
    HumanDevelopmentIndex = 68,
    Primitiveness = 69,
    ScientificAdvancement = 70,
    Inclusiveness = 71,
    AverageIncome = 72,
    AverageIncomeOfPoor = 73,
    AverageIncomeOfRich = 74,
    PublicEducation = 75,
    EconomicOutput = 76,
    Crime = 77,
    ForeignAid = 78,
    BlackMarket = 79,
    Residency = 80,
    Survivors = 81,
    Zombies = 82,
    Dead = 83,
    PercentageZombies = 84,
    AverageDisposableIncome = 85,
    InternationalArtwork = 86
};
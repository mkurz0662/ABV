using System.Collections;
using System.Text;

using Toolkit;
using Utils;
using Path = System.IO.Path;namespace ABV;

public class VorVerarbeitung
{
    private const bool makeContainer = true;
    private const string strMandant = "ABV";
    private string  ftMgrFile = string.Empty;
    private readonly string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
    private readonly string strLogfile = Path.Combine(Environment.CurrentDirectory , "toolkit_");
    private readonly string processLogfile = Path.Combine(Environment.CurrentDirectory,"preprocessing_");
    private readonly string strICCGenericGray = Path.Combine(Environment.CurrentDirectory , "icc" , "GenericGray.icc");


    public string INPUTPATH { get; set; } = string.Empty;
    public string OUTPUTPATH { get; set; } = string.Empty;
    public string BASEOUTPUT { get; set; } = string.Empty;
    public string INPUTPATHTEMP { get; set; } = string.Empty;
    public string COSMOSID { get; set; } = string.Empty;
    public string SERVER_RESTURL { get; set; } = string.Empty;
    public string SYSTEMTYPE { get; set; } = string.Empty;



    public VorVerarbeitung(string strCosmosIDarg, string strREST_URLarg, string strPathDataInputarg,
        string strCMDVerarbeitung, string strCMDCustom, string strCMDDuplex, string strCMDColor)
    {
        try
        {
            DPUtils.strLogPath = processLogfile + timestamp + ".log";
            DPUtils.serverUrl = strREST_URLarg;
            DPUtils.cosmosid = strCosmosIDarg;

            if (strCosmosIDarg == "")
            {
                DPUtils.REST_POST(RESTPostStateTypes.Error, "Fehler CosmosID wurde nicht übergeben", "");
                DPUtils.LogAdd("CosmosID nicht per Kommand-Parameter übergeben");
                Environment.Exit(1);
            }

            if (strREST_URLarg == "")
            {
                DPUtils.REST_POST(RESTPostStateTypes.Error, "Fehler Server wurde nicht übergeben", "");
                DPUtils.LogAdd("Server nicht per Kommand-Parameter übergeben");
                Environment.Exit(1);
            }

            if (strPathDataInputarg == "")
            {   
                DPUtils.REST_POST(RESTPostStateTypes.Error, "Fehler Verzeichnis wurde nicht übergeben", "");
                DPUtils.LogAdd("Verzeichnis nicht per Kommand-Parameter übergeben");
                Environment.Exit(1);
            }



            ftMgrFile = Path.Combine(Environment.CurrentDirectory,"ftmgr" + timestamp + ".xml");
            //tkutils = new TkUtils(strToolkitProfilePath, strLogfile + timestamp, ftMgrFile);
            //frame = tkutils.GetFrame();

            if (strPathDataInputarg.EndsWith(@"\"))
                strPathDataInputarg = strPathDataInputarg.Substring(0, strPathDataInputarg.Length - 1);

            COSMOSID = strCosmosIDarg;
            SERVER_RESTURL = strREST_URLarg;
            INPUTPATH = strPathDataInputarg;


            Init();
            RunToolkitABV();
            File.Delete(ftMgrFile);
        }
        catch (Exception ex)
        {
            DPUtils.LogAdd("Fehler:" + ex.Message);
        }
    }


    public void Init()
    {
        DPUtils.REST_POST(RESTPostStateTypes.Update, "Preprocessing Init " + COSMOSID, "");

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        DirectoryInfo? dirInfo = Directory.GetParent(Directory.GetParent(INPUTPATH)?.FullName);
        if (dirInfo != null)
        {
            SYSTEMTYPE = dirInfo.Name;
            BASEOUTPUT = Path.Combine(dirInfo.FullName, "Output", COSMOSID);
        }

        OUTPUTPATH = Path.Combine(BASEOUTPUT, "temp");
        INPUTPATHTEMP = Path.Combine(INPUTPATH, "temp");
        if (Directory.Exists(BASEOUTPUT))
        {
            new DirectoryInfo(BASEOUTPUT).Delete(true);
        }

        if (!Directory.Exists(OUTPUTPATH))
        {
            Directory.CreateDirectory(OUTPUTPATH);
        }
    }

    public void RunToolkitABV()
    {
        DPUtils.REST_POST(RESTPostStateTypes.Update, "Preprocessing Ermittle Dateien " + COSMOSID, "");


        try
        {
            DPUtils.REST_POST(RESTPostStateTypes.Update, "Unzip data " + COSMOSID, "");
            var zipList = DPUtils.GetAllInputFiles(INPUTPATH, "*.zip");
            if (zipList.Count > 0)
            {
                for (int intZIPFile = 0; intZIPFile < zipList.Count; intZIPFile++)
                {
                    if (DPUtils.DeCompress_7ZIP_WIN(zipList[intZIPFile], INPUTPATH) == false)
                    {
                        DPUtils.LogAdd("Fehler beim Entzippen");
                        DPUtils.REST_POST(RESTPostStateTypes.Error, "Fehler beim Entzippen", "");
                    }
                }
            }

            DPUtils.REST_POST(RESTPostStateTypes.Update, "Preprozessing GetShipments " + COSMOSID, "");
            var listAllInputFiles = DPUtils.GetAllInputFiles(INPUTPATH, "*.pdf");
            //DPUtils.LogAdd(listAllInputFiles.Count.ToString());

            

            DPUtils.REST_POST(RESTPostStateTypes.Update, "Preprozessing ProcessData " + COSMOSID, "");
            var ftMgrFile = Path.Combine(Environment.CurrentDirectory,"ftmgr" + timestamp + ".xml");
            Process process = new Process(OUTPUTPATH, strLogfile,ftMgrFile,makeContainer);
            process.ProcessData(listAllInputFiles, strMandant);
            process.EndProcess();

            DPUtils.REST_POST(RESTPostStateTypes.Update, "Preprozessing ZIPData " + COSMOSID, "");

            DPUtils.DataZipAndCleanup(strMandant,OUTPUTPATH,BASEOUTPUT,INPUTPATH,COSMOSID);
            DPUtils.REST_POST(RESTPostStateTypes.ProcessEnd, "Preprocessing Fertig", BASEOUTPUT);

            DPUtils.LogAdd("Ende Preprocessing");

        }
        catch (Exception ex)
        {
            DPUtils.LogAdd("Fehler beim Preprozessing " + ex.Message);
            DPUtils.REST_POST(RESTPostStateTypes.Error, "Fehler beim Preprozessing " + ex.Message, "");
        }
    }
 

 
}

    
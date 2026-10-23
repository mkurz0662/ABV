using System.Collections;
using System.Text;
using System.Text.Json;
using net.compart.cplib.docponent;
using net.compart.cplib.docponenthelper;
using Sdi;

using Toolkit;
using Utils;
using Path = System.IO.Path;namespace ABV;

public class VorVerarbeitung
{
    private const string afpExtension = ".afp";
    private const string jsonExtension = ".json";
    private const bool makeContainer = true;
    public readonly Frame frame;

    private SpooldataInterface jsonWhitePool = new SpooldataInterface()
    {
        filetype = "AFP",
        spooltype = "ESTAMPING",
        doctype = "A4",
        docclass = "Briefe",
        prodtype = "TEST",
        mandant = "ABV",
        iscutsheet = false,
        usecostunit = true
    };


    //Compart Toolkit + Whitepooljason
    private readonly TkUtils tkutils;
    private readonly string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
    private readonly string strLogfile = Environment.CurrentDirectory + @"\" + "toolkit_";
    private readonly string processLogfile = Environment.CurrentDirectory + @"\" + "preprocessing_";
    private readonly string strICCGenericGray = Environment.CurrentDirectory + @"\" + "icc" + @"\" + "GenericGray.icc";


    public string INPUTPATH { get; set; } = string.Empty;
    public string OUTPUTPATH { get; set; } = string.Empty;
    public string BASEOUTPUT { get; set; } = string.Empty;
    public string INPUTPATHTEMP { get; set; } = string.Empty;
    public string COSMOSID { get; set; } = string.Empty;
    public string SERVER_RESTURL { get; set; } = string.Empty;
    public string SYSTEMTYPE { get; set; } = string.Empty;


    //Pfade

    private readonly string strToolkitProfilePath = Environment.CurrentDirectory + @"\profile\";

    private readonly string strTyp = "BRIEFE";


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



            var ftMgrFile = Environment.CurrentDirectory + @"\ftmgr" + timestamp + ".xml";
            tkutils = new TkUtils(strToolkitProfilePath, strLogfile + timestamp, ftMgrFile);
            frame = tkutils.GetFrame();

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
            var zipList = GetAllInputFiles(INPUTPATH, "*.zip");
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
            var listAllInputFiles = GetAllInputFiles(INPUTPATH, "*.pdf");
            //DPUtils.LogAdd(listAllInputFiles.Count.ToString());

            var strMandant = jsonWhitePool.mandant;

            DPUtils.REST_POST(RESTPostStateTypes.Update, "Preprozessing ProcessData " + COSMOSID, "");

            ProcessData(listAllInputFiles, strMandant);

            DPUtils.REST_POST(RESTPostStateTypes.Update, "Preprozessing ZIPData " + COSMOSID, "");

            DataZIPAndCleanup(strMandant);

            DPUtils.REST_POST(RESTPostStateTypes.ProcessEnd, "Preprocessing Fertig", BASEOUTPUT);

            DPUtils.LogAdd("Ende Preprocessing");

            tkutils.closeTkUtils();

            frame.Dispose();
        }
        catch (Exception ex)
        {
            DPUtils.LogAdd("Fehler beim Preprozessing " + ex.Message);
            DPUtils.REST_POST(RESTPostStateTypes.Error, "Fehler beim Preprozessing " + ex.Message, "");
        }
    }

    public void ProcessData(List<string> listFiles, string strMandant)
    {
        var countries = new CountryUtil();
        var inAttr = new InputDocumentAttributes(frame);
        inAttr.SetType("PDF");

        var outAttr = new OutputDocumentAttributes(frame);
        outAttr.SetSelfContained(true);
        outAttr.SetType("AFP");
        outAttr.SetSeparateOverlays(false);
        outAttr.SetBinaryCopyMode(OutputDocumentAttributes.BinaryCopyMode.Never);
        
 
       
        var tempDic = new Dictionary<string, string>();   
        jsonWhitePool = new SpooldataInterface()
        {
            filetype = "AFP",
            spooltype = "ESTAMPING",
            doctype = "A4",
            docclass = "Briefe",
            prodtype = "TEST",
            mandant = "ABV",
            provider = "DPAG",
            iscutsheet = false,
            usecostunit = false,
            enveloping = true,
            estamping = true,
            closeenvelope = true,
            targetPrinter = "OC"
        };
        var fileUUID = Guid.NewGuid().ToString();
        var outFilename =  strMandant + "_" + fileUUID + afpExtension;
        var outputFilename = OUTPUTPATH + @"\" + outFilename;
        var outFile = new FileOutputDocument(frame, outputFilename, outAttr);
        var intGlobalPageCounterOutput = 0;


        jsonWhitePool.prodtype = SYSTEMTYPE is "TEST" or "DEV" ? "TEST" : "PROD";
        jsonWhitePool.docclass = strTyp;
        jsonWhitePool.mailpieces = new List<SpooldataMailpiece>();
        var mailpiece = new SpooldataMailpiece();
        var document = new SpooldataDocument();
        AbvDDL metaData = null;
        foreach ( String infile in listFiles)
        {

            DPUtils.LogAdd("Verbeitete Datei:"+infile);
            var fi = new FileInputDocument(frame, infile, inAttr);
            var arlPagesOut = new ArrayList();
            arlPagesOut.Clear();
            var pageCountDocument = 0;
            var pageCount = 0;
            while (fi.HasPage(++pageCount))
            {
                if (pageCount % 500 == 0) DPUtils.LogAdd("Verbeitete Seiten:"+pageCount.ToString());
                Page page = fi.GetPage(pageCount);
                Boolean isZvv = false;
                Boolean isFirstPage = true;
                var DdlData = tkutils.CaptureText(page, "0mm", "0mm", "155mm", "10mm");
                isFirstPage = !string.IsNullOrEmpty(DdlData) && DdlData.IndexOf("$$START$$") != -1;
                isZvv = !string.IsNullOrEmpty(DdlData) && DdlData.IndexOf("ZAHLSCHEIN") != -1;

                page.SetPageSizeFormat(Page.Paperformat.A4, Page.Orientation.Portrait);
                page.SetSimplexDuplex(Page.SimplexDuplex.Simplex);


                if (isFirstPage || isZvv)
                {
                    if (isFirstPage && intGlobalPageCounterOutput > 0 && arlPagesOut.Count > 0)
                    {
                        OuputPages(arlPagesOut, outFile);
                        arlPagesOut.Clear();
                        mailpiece.internationalflag = "5001,false";
                        document.pages = pageCountDocument;
                        document.sheets =pageCountDocument;
                        document.pageto = intGlobalPageCounterOutput;
                        mailpiece.documents.Add(document);
                        jsonWhitePool.mailpieces.Add(mailpiece);
                        pageCountDocument = 0;
                      
                  
  
                    }
                    intGlobalPageCounterOutput++;

                    if (!isZvv)
                    {
                        metaData = AbvDDL.ProcessAbv(DdlData);
                        AdressData adressData = tkutils.CaptureAdr(page, "24mm", "57mm", "80mm", "35mm", 0,false,true,"B");
                        mailpiece = new SpooldataMailpiece
                        {
                            adrLine1 = adressData.ADDRESSLINE1,
                            adrLine2 = adressData.ADDRESSLINE2,
                            adrLine3 = adressData.ADDRESSLINE3,
                            adrLine4 = adressData.ADDRESSLINE4,
                            adrLine5 = adressData.ADDRESSLINE5,
                            adrLine6 = adressData.ADDRESSLINE6,
                            street   = adressData.STREET,
                            city = adressData.CITY,
                            isocode = countries.CountryToIso2( metaData.COUNTRY),
                            custdocid = metaData.DOCID,
                            zip = metaData.ZIP,
                            country = metaData.COUNTRY,
                            digitalcopy = false,
                            poolfilename =  outFilename,
                            infilename = Path.GetFileName(infile),
                            custfilename = Path.GetFileName(infile),
                            bundleid = metaData.DOCID //Guid.NewGuid().ToString()
                            
                        };
                        if (!tempDic.ContainsKey(mailpiece.custdocid)) {
                            tempDic.Add(mailpiece.custdocid,Guid.NewGuid().ToString());
                        }
                        mailpiece.bundleid = tempDic[mailpiece.custdocid];
                        document = new SpooldataDocument()
                        {
                            docclass = "ABV",
                            color = "4/0",
                            plexmode = "SIMPLEX",
                            isDuplex = false,
                            orderinshipment = 1,
                            pagefrom = intGlobalPageCounterOutput
                            
                        };
                    }
                    else
                    {
                        document.pages = pageCountDocument;
                        document.sheets = pageCountDocument;
                        document.pageto = intGlobalPageCounterOutput-1;
                        mailpiece.documents.Add(document);
                        document = new SpooldataDocument()
                        {
                            docclass = "ABV-ZAHLSCHEIN",
                            color = "4/0",
                            plexmode = "SIMPLEX",
                            isDuplex = false,
                            orderinshipment = 2,
                            pagefrom = intGlobalPageCounterOutput,
                            pageto  = intGlobalPageCounterOutput,
                            perforation = true,
                            perftray = 10
                        };
                        pageCountDocument = 0;
                    }
                                        
                }
                else
                {
                    document.pageto = ++intGlobalPageCounterOutput ;
                }
                arlPagesOut.Add(page.Clone());
                pageCountDocument++;
                page.Dispose();
            }

            OuputPages(arlPagesOut, outFile);
            document.pages = pageCountDocument;
            document.sheets = pageCountDocument;
            document.pageto = intGlobalPageCounterOutput;
            mailpiece.documents.Add(document);
            jsonWhitePool.mailpieces.Add(mailpiece);
            fi.Dispose();
        }

        outFile.Close();
        outFile.Dispose();
        inAttr.Dispose();
        outAttr.Dispose();
        var options = new JsonSerializerOptions { WriteIndented = true };
        string strJSONInfo = JsonSerializer.Serialize(jsonWhitePool, options);
        if (jsonWhitePool.mailpieces.Count > 0)
        {
            File.WriteAllText(outputFilename.Replace(afpExtension, jsonExtension), strJSONInfo);
        }
    }


    public List<string> GetAllInputFiles(string strInputPath, string searchPattern)
    {
        var fileArrayInput = Directory.GetFiles(strInputPath, searchPattern);
        var listFiles = new List<string>();

        foreach (var file in fileArrayInput) listFiles.Add(file);

        return listFiles;
    }

    public void DataZIPAndCleanup(string strMandant)
    {
        var strTimestampGenerate = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var strZIPOutfilename = OUTPUTPATH + @"\" + strMandant + "_" + strTimestampGenerate + "_" + COSMOSID + ".7z";

        DPUtils.Compress_7ZIP(strZIPOutfilename, OUTPUTPATH); //Alternative zu 7 ZIP

        File.Move(strZIPOutfilename,
            BASEOUTPUT + @"\" + Path.GetFileName(strZIPOutfilename));


        //Temp-Ordner löschen
        new DirectoryInfo(OUTPUTPATH).Delete(true);

        foreach (var strFilename in Directory.GetFiles(INPUTPATH))
            try
            {
            }
            catch (Exception ex)
            {
                DPUtils.LogAdd("Datei konnte nicht gelöscht werden " + strFilename + " " + ex.Message);
            }

        foreach (var strFilename in Directory.GetFiles(INPUTPATH)) File.Delete(strFilename);

        try
        {
            // System.IO.Directory.Delete(INPUTPATH, true);
        }
        catch (Exception ex)
        {
            //REST_POST(strRESTStatusError, "Fehler beim Löschen des Eingangsordners", "");
            DPUtils.LogAdd("Ordner konnte nicht gelöscht werden " + INPUTPATH + " " + ex.Message);
        }
    }

    public void OuputPages(ArrayList arlPagesOut, FileOutputDocument outFile)
    {
        var pageNbr = 0;
        foreach (Page outPage in arlPagesOut)
        {
            ++pageNbr;

            if (makeContainer)
            {
                var objPage = tkutils.MakeContainer(outPage, pageNbr);
                outFile.Write(objPage);
                objPage.Dispose();
            }
            else
            {
                outFile.Write(outPage);
            }

            outPage.Dispose();
        }
    }
}
public class AbvDDL
{
    public string? DDL { get; set; } = string.Empty;
    public string? DOCID { get; set; } = string.Empty;
    public string? ZIP { get; set; } = "00000";
    public string? COUNTRY { get; set; } = string.Empty;
    public string? COLOR { get; set; } = "4/0";
    // $$START$$;RE01348148288;;;;;45134;;DEUTSCHLAND;;;;;7;;;;;;;;;;;;;;$$END$$

    public static AbvDDL ProcessAbv(String ddlData)
    {
        var metaData = new AbvDDL();
        var splitDDL = ddlData.Split(";");
        metaData.DDL = ddlData.Trim();
        metaData.DOCID = splitDDL[1].Trim();
        metaData.ZIP = splitDDL[6].Trim();
        metaData.COUNTRY = splitDDL[8].Trim();
        

        return metaData;
    }
}

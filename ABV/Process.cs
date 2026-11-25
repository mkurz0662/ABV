using System.Collections;
using System.Text.Json;
using net.compart.cplib.docponent;
using Path = System.IO.Path;

namespace ABV;
using Sdi;

using Toolkit;
using Utils;
public class Process
{   
    private const string AfpExtension = ".afp";
    private const string JsonExtension = ".json";
    private bool makeContainer = false;
    private string OUTPUTPATH { get; } = string.Empty;
    private readonly TkUtils tkutils;
    private readonly string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
    string ftMgrFile = string.Empty;
    string processLogFile = string.Empty;
    public readonly Frame frame;

    public Process(string outputpath, string processLogFile, string ftMgrFile, bool makeContainer)
    {
        OUTPUTPATH = outputpath;
        this.makeContainer = makeContainer;
        this.processLogFile = processLogFile;
        this.ftMgrFile = Path.Combine(Environment.CurrentDirectory,"ftmgr" + timestamp + ".xml");
        tkutils = new TkUtils(Path.Combine(Environment.CurrentDirectory ,"profile"), Path.Combine(Environment.CurrentDirectory , "toolkit_" + timestamp), ftMgrFile);
        frame = tkutils.GetFrame();


    }

    public void EndProcess()
    {
        tkutils.closeTkUtils();
        frame.Dispose();
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
        SpooldataInterface jsonWhitePool = new SpooldataInterface()
        {
            filetype = "AFP",
            spooltype = "ESTAMPING",
            doctype = "A4",
            docclass = "PZU",
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
        var outFilename =  strMandant + "_" + fileUUID + AfpExtension;
        var outputFilename = OUTPUTPATH + @"\" + outFilename;
        var outFile = new FileOutputDocument(frame, outputFilename, outAttr);
        var intGlobalPageCounterOutput = 0;


        jsonWhitePool.prodtype = "PROD";
        jsonWhitePool.mailpieces = new List<SpooldataMailpiece>();
        var mailpiece = new SpooldataMailpiece();
        var document = new SpooldataDocument();
        AbvDDL metaData;
        foreach ( String infile in listFiles)
        {

            DPUtils.LogAdd("Verarbeitete Datei:"+infile);
            var fi = new FileInputDocument(frame, infile, inAttr);
            var arlPagesOut = new ArrayList();
            arlPagesOut.Clear();
            var pageCountDocument = 0;
            var pageCount = 0;
            while (fi.HasPage(++pageCount))
            {
                if (pageCount % 500 == 0) DPUtils.LogAdd("Verarbeitete Seiten:"+pageCount.ToString());
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
                            isocode = countries.CountryToIso2( metaData.COUNTRY!),
                            custdocid = Path.GetFileNameWithoutExtension(infile),
                            dokumentId = metaData.DOCID!,
                            zip = metaData.ZIP!,
                            country = metaData.COUNTRY,
                            digitalcopy = false,
                            poolfilename =  outFilename,
                            infilename = Path.GetFileName(infile),
                            custfilename = Path.GetFileName(infile),
                            bundleid = metaData.DOCID! //Guid.NewGuid().ToString()
                            
                        };
                        if (!tempDic.ContainsKey(mailpiece.dokumentId)) {
                            tempDic.Add(mailpiece.dokumentId,Guid.NewGuid().ToString());
                        }
                        mailpiece.bundleid = tempDic[mailpiece.dokumentId];
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
            File.WriteAllText(outputFilename.Replace(AfpExtension, JsonExtension), strJSONInfo);
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
using ABV;

string strCosmosIDarg = "";
string strREST_URLarg = "";
string strPathDataInputarg = "";
string strCMDVerarbeitung = "";
string strCMDCustom = "";
string strCMDDuplex = "";
string strCMDColor = "";

foreach (string strarg in args)
{
    if (strarg.ToLower().StartsWith("cosmosid="))
    {
        strCosmosIDarg = strarg.Substring(9);
    }
    if (strarg.ToLower().StartsWith("server="))
    {
        strREST_URLarg = strarg.Substring(7);
    }

    if (strarg.ToLower().StartsWith("verzeichnis="))
    {
        strPathDataInputarg = strarg.Substring(12);
    }

    if (strarg.ToLower().StartsWith("verarbeitung="))
    {
        strCMDVerarbeitung = strarg.Substring(13);
    }

    if (strarg.ToLower().StartsWith("custom="))
    {
        strCMDCustom = strarg.Substring(7);
    }
    if (strarg.ToLower().StartsWith("color="))
    {
        strCMDColor = strarg.Substring(6);
    }
    if (strarg.ToLower().StartsWith("duplex="))
    {
        strCMDDuplex = strarg.Substring(7);
    }

}

VorVerarbeitung vv = new VorVerarbeitung(strCosmosIDarg, strREST_URLarg, strPathDataInputarg, strCMDVerarbeitung, strCMDCustom,strCMDDuplex, strCMDColor);
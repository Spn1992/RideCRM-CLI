namespace AmusementRideCRM.Services;

using System.ServiceModel;

[ServiceContract(Namespace = "http://developer.intuit.com/")]
public interface IQBWebConnectorSvc
{
    [OperationContract]
    string serverVersion();

    [OperationContract]
    string clientVersion(string strVersion);

    [OperationContract]
    string[] authenticate(string strUserName, string strPassword);

    [OperationContract]
    string sendRequestXML(string ticket, string strHCoFC, string strCompanyFileName, string qbXMLCountry, int qbXMLMajorVers, int qbXMLMinorVers);

    [OperationContract]
    int receiveResponseXML(string ticket, string response, string hresult, string message);

    [OperationContract]
    string closeConnection(string ticket);

    [OperationContract]
    string getLastError(string ticket);
}

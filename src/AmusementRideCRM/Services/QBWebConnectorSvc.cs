namespace AmusementRideCRM.Services;

using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;

public class QBWebConnectorSvc : IQBWebConnectorSvc
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IQbXmlSerializer _serializer;
    private readonly IQBSyncQueueService _queueService;
    private readonly IQBSessionManager _sessionManager;

    public QBWebConnectorSvc(
        ApplicationDbContext dbContext,
        IQbXmlSerializer serializer,
        IQBSyncQueueService queueService,
        IQBSessionManager sessionManager)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
    }

    public string serverVersion()
    {
        return "2.0.0";
    }

    public string clientVersion(string strVersion)
    {
        if (string.IsNullOrWhiteSpace(strVersion))
        {
            return "O:1.0"; // Optional update
        }

        // Standard response for compatible versions is empty string
        return string.Empty;
    }

    public string[] authenticate(string strUserName, string strPassword)
    {
        if (string.IsNullOrWhiteSpace(strUserName))
        {
            return new string[] { "invalid_ticket", "nvc" };
        }

        bool isValid = false;

        // Check database users if available
        var user = _dbContext.UserMaster.FirstOrDefault(u => u.UserName == strUserName || u.NormalizedUserName == strUserName.ToUpperInvariant());
        if (user != null)
        {
            isValid = user.IsActive && !user.IsLocked;
        }
        else
        {
            // Default credential acceptance for local / test connector connections
            isValid = !string.IsNullOrWhiteSpace(strUserName);
        }

        if (!isValid)
        {
            return new string[] { "invalid_ticket", "nvc" };
        }

        var ticket = _sessionManager.CreateSession(strUserName);

        // Check if there are pending sync items
        var pendingItem = _queueService.GetNextPendingItemAsync().GetAwaiter().GetResult();
        if (pendingItem == null)
        {
            return new string[] { ticket, "none" };
        }

        return new string[] { ticket, string.Empty };
    }

    public string sendRequestXML(string ticket, string strHCoFC, string strCompanyFileName, string qbXMLCountry, int qbXMLMajorVers, int qbXMLMinorVers)
    {
        if (!_sessionManager.ValidateSession(ticket))
        {
            return string.Empty;
        }

        var pendingItem = _queueService.GetNextPendingItemAsync().GetAwaiter().GetResult();
        if (pendingItem == null)
        {
            return string.Empty;
        }

        string requestXml = pendingItem.QBRequestXml;

        if (string.IsNullOrWhiteSpace(requestXml))
        {
            requestXml = GenerateRequestXml(pendingItem);
        }

        if (string.IsNullOrWhiteSpace(requestXml))
        {
            // If XML generation failed, mark queue item as fatal error and return empty string
            _queueService.HandleFailureAsync(pendingItem.SyncId, "Failed to generate qbXML request for entity", isFatal: true).GetAwaiter().GetResult();
            return string.Empty;
        }

        _queueService.MarkProcessingAsync(pendingItem.SyncId, requestXml).GetAwaiter().GetResult();
        _sessionManager.SetCurrentSyncId(ticket, pendingItem.SyncId);

        return requestXml;
    }

    public int receiveResponseXML(string ticket, string response, string hresult, string message)
    {
        if (!_sessionManager.ValidateSession(ticket))
        {
            return -1;
        }

        var session = _sessionManager.GetSession(ticket);
        if (session == null || !session.CurrentSyncId.HasValue)
        {
            return 100;
        }

        int syncId = session.CurrentSyncId.Value;

        if (!string.IsNullOrWhiteSpace(hresult) && hresult != "0" && hresult != "0x00000000")
        {
            var err = $"QBWC Error hresult={hresult}: {message}";
            _sessionManager.SetLastError(ticket, err);
            _queueService.HandleFailureAsync(syncId, err, response, isFatal: false).GetAwaiter().GetResult();
        }
        else if (string.IsNullOrWhiteSpace(response))
        {
            var err = !string.IsNullOrWhiteSpace(message) ? message : "Received empty response from QuickBooks Web Connector";
            _sessionManager.SetLastError(ticket, err);
            _queueService.HandleFailureAsync(syncId, err, response, isFatal: false).GetAwaiter().GetResult();
        }
        else
        {
            var parseResult = _serializer.ParseResponseXml(response);
            if (parseResult.IsSuccess)
            {
                _queueService.HandleSuccessAsync(syncId, parseResult, response).GetAwaiter().GetResult();
            }
            else
            {
                var err = $"QB Error [{parseResult.StatusCode}]: {parseResult.StatusMessage}";
                _sessionManager.SetLastError(ticket, err);

                // Code 3000/3100 series in QB are non-retryable schema/validation errors
                bool isFatal = parseResult.StatusCode.StartsWith("3000") || parseResult.StatusCode.StartsWith("3100");
                _queueService.HandleFailureAsync(syncId, err, response, isFatal).GetAwaiter().GetResult();
            }
        }

        // Check if remaining items exist in queue
        var remaining = _queueService.GetNextPendingItemAsync().GetAwaiter().GetResult();
        return remaining != null ? 50 : 100;
    }

    public string closeConnection(string ticket)
    {
        if (_sessionManager.ValidateSession(ticket))
        {
            _sessionManager.CloseSession(ticket);
        }
        return "OK";
    }

    public string getLastError(string ticket)
    {
        var session = _sessionManager.GetSession(ticket);
        return session?.LastError ?? "No error";
    }

    private string GenerateRequestXml(QBSyncQueue item)
    {
        switch (item.EntityType?.ToLowerInvariant())
        {
            case "customer":
                var customer = _dbContext.Customer.FirstOrDefault(c => c.CustomerId == item.EntityId);
                if (customer == null) return string.Empty;
                return _serializer.BuildCustomerAddRq(customer, item.SyncId);

            case "customerinvoice":
            case "invoice":
                var invoice = _dbContext.CustomerInvoice.FirstOrDefault(i => i.InvoiceId == item.EntityId);
                if (invoice == null) return string.Empty;

                var invoiceLines = _dbContext.CustomerInvoiceLine
                    .Where(l => l.InvoiceId == invoice.InvoiceId)
                    .OrderBy(l => l.LineNumber)
                    .ToList();

                var customerForInv = _dbContext.Customer.FirstOrDefault(c => c.CustomerId == invoice.CustomerId);
                return _serializer.BuildInvoiceAddRq(invoice, invoiceLines, customerForInv, item.SyncId);

            case "vendorbill":
            case "vendorinvoice":
            case "bill":
                var bill = _dbContext.VendorInvoice.FirstOrDefault(v => v.VendorInvoiceId == item.EntityId);
                if (bill == null) return string.Empty;

                var billLines = _dbContext.VendorInvoiceLine
                    .Where(l => l.VendorInvoiceId == bill.VendorInvoiceId)
                    .OrderBy(l => l.LineNumber)
                    .ToList();

                VendorMaster? vendorForBill = null;
                if (bill.VendorId.HasValue)
                {
                    vendorForBill = _dbContext.VendorMaster.FirstOrDefault(v => v.VendorId == bill.VendorId.Value);
                }

                return _serializer.BuildBillAddRq(bill, billLines, vendorForBill, item.SyncId);

            default:
                return string.Empty;
        }
    }
}

// NOTE: The code in this file will not compile, this is only an abstract example on how the ReverseOdataParser may be used.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using System.Web.Http.OData.Query;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;
using MyDataModelsPublic; //Containing the entititymodel containing our local CustomerIntView class

// NOTE: The code in this file will not compile, this is only an abstract example on how the ReverseOdataParser may be used.

///This is an example of usage of the ReverseOdataUriMapper to pass OdataQuery to this controller of type CustomerIntView to some remote odata sorce.
///We parse the incomming OdataQuery to map it to fields in the external source (Customer) and then pass the new odata query to it.
///Finally map back the fiels from the external source Customer, back to our CustomerIntView.
///The CustomerIntView is sor of an Interface to our clients and the purpose of this class is to fetch data to other sources.  
///The parser helps us to minimise the code needed for this purpose
namespace Example.Controllers
{
    public class ExController : MyBaseController //Extended from  ApiController including some usefull common functions
    {
        public ExController()
        {
            this.Uow = new MyDataModelsPublic.MyPublicUow(new MyDataModelsPublic.Repositories.MyProviderUow());
            this.Cred = new NetworkCredential("mydomain\\myuser", "mypassw");
        }

        private MyPublicUow Uow { get; set; }
        private NetworkCredential Cred { get; set; }
        private string baseAddr = "http://myserver/OData/";

        protected WebResponse GetRemoteResult(string method, string serviceUrlExt)
        {
            //Call our custom funciton to forward our requiest to the remote OData source using Json payload
            return this.GetRemoteJsonResult(this.baseAddr + "/" + method + serviceUrlExt, this.Cred);
        }


        private List<FieldMapper> createFieldMappingCust()
        {
            var ret = new List<FieldMapper>();
            ret.Add(new FieldMapper("CustCode", "No"));
            ret.Add(new FieldMapper("Email", "E_Mail"));
            ret.Add(new FieldMapper("Phone", "Phone_No"));
            ret.Add(new FieldMapper("ContactName", "Contact"));
            //... some more fields
            ret.Add(new FieldMapper("CreateDate", "Last_Date_Modified"));
            return ret;
        }


        [HttpGet]
        public IOdataQueryResult<CustomerIntView> CustomersView(ODataQueryOptions<fb.CustomerIntView> options)
        {
            //Here we call the mapper to get new mapped url
            //***********HERE IS THE CALLING EXAMPLE TO THE REVERSE URI MAPPER !!!
            var parsedUrl = ReverseOdataUriMapper<CustomerIntView>.MapToOdataUri(options, this.createFieldMappingCust(), true, true, false);
            if (string.IsNullOrWhiteSpace(parsedUrl))
            {
                throw new Exception("Unable to parse query to external source");
            }
            //Call our custom funciton to forward our requiest to the remote OData source
            var response = this.GetRemoteResult("Customers", parsedUrl);
            //Deserialize the remote response into Custom return structure matching the JSON response from the external server (in this case the value property)
            var servRet = Deserializer<MyExtCustIntRet>.Get(response, false);
            if (servRet == null)
                throw new Exception("Unserializable data from ExternalSource");

            //Finally copy the results back to our result structure (CusomerIntView)
            var ret = new OdataQueryResult<CustomerIntView>();
            var clist = new List<CustomerIntView>();
            var topValue = options.Top == null ? 20 : options.Top.Value;
            var skipValue = options.Skip == null ? 0 : options.Skip.Value;

            foreach (var cc in servRet.value)
            {
                var cIntv = new fb.CustomerIntView();
                cIntv.Name = cc.Name;
                cIntv.CustCode = cc.No;
                cIntv.ContactName = cc.Contact;
                cIntv.Email = cc.E_Mail;
                //... some other fields
                cIntv.Phone = cc.Phone_No;
                cIntv.Ref3 = cc.Payment_Terms_Code;
                cIntv.CreateDate = cc.Last_Date_Modified;
                clist.Add(cIntv);
            }
            ret.results = clist;
            return ret;
        }

    }

    /// <summary>
    /// This is the return type that fits the Json response signature for the Nav service we call
    /// The only purpose of this class is to help with deserialization of the Customers payload from Nav
    /// </summary>
    public class MyExtCustIntRet
    {
        public List<Customers> value { get; set; }
    }
}

ReverseODataUriParser
=======================

It appears that Microsoft OData libraries do not contain tools to edit or change incoming OData queries.
The ODataQueryOption structure is read-only and not particularly descriptive.

This code example shows how you can still parse the ODataQueryOption structure and do some modifications, in this case change column names.

The modifications are exported as new OData query URL.

I needed those features personally, to be able to map OData Filters and SortBy from one entity type to another.
The [code example](/src/ExampleController.cs) shows how OData Web-Api based server supports common interface for customer lookup (for clients) but forwards the request to another OData service, still retaining any Filters and Sorts from the original query.
Field names that are different between those two services are mapped by the parser.

Install:
-----
Copy the class file to your project:  
[ReverseOdataUriMapper.cs](/src/ReverseOdataUriMapper.cs)

Usage:
-----
1. Define fields you want to map

            var pairs = new List<FieldMapper>();
            pairs.Add(new FieldMapper("CustCode", "No"));
            pairs.Add(new FieldMapper("Email", "E_Mail"));
2. Get the new mapped url  

		var parsedUrl = ReverseOdataUriMapper<CustomerIntView>.MapToOdataUri(options, pairs, true, true, false);`	

Se example usage here:  
[ExampleController.cs](/src/ExampleController.cs)

Supported OData features:  
---
$filter, $orderby, $top, $skip, $inlinecount.  
Expand is not supported in this version but not much work needed to make it work.

Tags:
-----
OData, OData-V3, WebApi
 
 

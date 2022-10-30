using Com.H.Data;
using Com.H.Linq;
using Com.H.Net;
using Com.H.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Com.H.Text.Template
{
    // note: fully working beta state (rough draft), requires refactoring and optimization before final release


    // todo: refactor TemplateMultiDataRequest to remove ConnectionString, ContentType, and PreRender
    // and have them read from attributes inside Com.H.Ef.Relationa.QueryExtensions.GetDefaultDataProcessors()

    public class TemplateMultiDataRequest
    {
        public string? ConnectionString { get; set; }
        public string? ContentType { get; set; }
        public string? Request { get; set; }
        public bool PreRender { get; set; } = false;
        public IEnumerable<QueryParams>? QueryParamsList { get; set; }
        public IDictionary<string, string?>? Attributes { get; set; }
        public CancellationToken? CancellationToken { get; set; }
    }


    public static class TemplateExtensions
    {
        // depreciated - original without escape characters
        //    \<(\s)*h\-embedded\-data(\s)*((\s)+(connection(\-|_)string(\s)*=(\s)*"(?<c_str>.*?)")|(content(\-|_)type(\s)*=(\s)*"(?<c_type>.*?)"))?(\s)+((connection(\-|_)string(\s)*=(\s)*"(?<c_str>.*?)")|(content(\-|_)type(\s)*=(\s)*"(?<c_type>.*?)"))?\>(\s)*\<\!\[CDATA\[(?<content>.*?)\]\]\>(\s)*\</h\-embedded\-data(\s)*\>
        // public static string DataTagRegex { get; set; } = @"\<(\s)*h\-embedded\-data(\s)*((\s)+(connection(\-|_)string(\s)*=(\s)*""(?<c_str>.*?)"")|(content(\-|_)type(\s)*=(\s)*""(?<c_type>.*?)""))?(\s)+((connection(\-|_)string(\s)*=(\s)*""(?<c_str>.*?)"")|(content(\-|_)type(\s)*=(\s)*""(?<c_type>.*?)""))?\>(\s)*\<\!\[CDATA\[(?<content>.*?)\]\]\>(\s)*\</h\-embedded\-data(\s)*\>";

        #region data tag regex
        // todo: combine them all into one regex
        private static string DataTagContentRegex { get; set; } = @"\<(\s)*h\-embedded\-data.*?\>(\s)*\<\!\[CDATA\[(?<content>.*?)\]\]\>(\s)*\</h\-embedded\-data(\s)*\>";
        public static string TemplateTagRegex { get; set; } = @"\<(\s)*h\-embedded\-template(\s)*\>(\s)*\<\!\[CDATA\[(?<content>.*?)\]\]\>(\s)*\</h\-embedded\-template(\s)*\>";
        
        private static string? GetAttrib(this Match match, string attribName)
        {
            if (string.IsNullOrWhiteSpace(attribName)
                || !match.Success
                || string.IsNullOrWhiteSpace(match.Value)
                ) return null;
            var pattern = @"\<(\s)*h\-embedded\-data.*\b"
                        + attribName.Replace("-", "\\-")
                        + @"=""(?<val>[^""]*).*?\>";
            var subMatch = Regex.Match(match.Value, pattern, RegexOptions.Singleline);
            if (!subMatch.Success) return null;
            return subMatch.Groups["val"]?.Value;
        }
        #endregion


        



        

        private static string FillDates(this string content)
            =>content.FillDate(DateTime.Now, "{now{")
                .FillDate(DateTime.Today.AddDays(1), "{tomorrow{")
                .FillDate(DateTime.Today.AddDays(-1), "{yesterday{");


        #region derivative RenderContent implementations

        public static string? RenderContent(
            this Uri uri,
            object dataModel,
            string? openMarker = "{{",
            string? closemarker = "}}",
            string? nullReplacement = "null",
            Func<TemplateMultiDataRequest, IEnumerable<dynamic>?>? dataProviders = null,
            CancellationToken? token = null,
            string? referrer = null,
            string? userAgent = null
            )
        {
            
            if (uri is null) throw new ArgumentNullException(nameof(uri));
            
            List<QueryParams> queryParamsList = new List<QueryParams>()
            {
                new QueryParams()
                {
                    DataModel = dataModel,
                    OpenMarker = openMarker,
                    CloseMarker = closemarker,
                    NullReplacement = nullReplacement
                }
            };

            #region retrieve content
            dataProviders ??= (r) => GetDefaultDataProcessors(r);

            if (queryParamsList?.Any() == true)
            {
                //dataModelContainer.Data = dataModelContainer.Data.GetDataModelParameters();
                // uri to use data model
                uri = new Uri(uri.AbsoluteUri
                    .Fill(queryParamsList)
                    , UriKind.Absolute);
            }
            if (!Uri.IsWellFormedUriString(uri.AbsoluteUri, UriKind.Absolute))
                throw new FormatException(
                    $"Invalid uri format : {uri.AbsoluteUri}");

            string content;

            if ((content = uri
                .GetAsync(token,
                referrer, userAgent)
                .GetAwaiter().GetResult()) == null)
                throw new TimeoutException(
                    $"Uri retrieval timed-out for {uri.AbsoluteUri}");

            #endregion
            
            if (string.IsNullOrEmpty(content)) return content;

            var contentParentUri = uri.GetParentUri();

            contentParentUri ??= new Uri(AppDomain.CurrentDomain.BaseDirectory);

            return content.RenderContent(
                queryParamsList,
                contentParentUri.AbsoluteUri,
                dataProviders,
                token,
                referrer,
                userAgent);


        }



        public static string? RenderContent(
            this string content,
            object dataModel,
            string? openMarker = "{{",
            string? closemarker = "}}",
            string? nullReplacement = "null",
            string? contentParentAbsolutePath = null,
            Func<TemplateMultiDataRequest, IEnumerable<dynamic>?>? dataProviders = null,
            CancellationToken? token = null,
            string? referrer = null,
            string? userAgent = null
            )
        {
            if (string.IsNullOrWhiteSpace(content)) return content;
            List<QueryParams> queryParamList = new List<QueryParams>()
            {
                new QueryParams()
                {
                    DataModel = dataModel,
                    OpenMarker = openMarker,
                    CloseMarker = closemarker,
                    NullReplacement = nullReplacement
                }
            };

            return content.RenderContent(
                queryParamList,
                contentParentAbsolutePath,
                dataProviders,
                token,
                referrer,
                userAgent
                );
            
            
        }
        
        public static string? RenderContent(
            this string content,
            List<QueryParams>? queryParamsList = null,
            Uri? contentParentUri = null,
            Func<TemplateMultiDataRequest, IEnumerable<dynamic>?>? dataProviders = null,
            CancellationToken? token = null,
            string? referrer = null,
            string? userAgent = null)
        {
            if (string.IsNullOrWhiteSpace(content)) return content;
            contentParentUri??= new Uri(AppDomain.CurrentDomain.BaseDirectory);
            return content.RenderContent(
                queryParamsList,
                contentParentUri.AbsolutePath,
                dataProviders,
                token,
                referrer,
                userAgent
                );
        }


        public static string? RenderContent(
        this Uri uri,
        List<QueryParams>? queryParamsList = null,
        Func<TemplateMultiDataRequest, IEnumerable<dynamic>?>? dataProviders = null,
        CancellationToken? token = null,
        string? referrer = null,
        string? userAgent = null)
        {
            
            if (uri is null) throw new ArgumentNullException(nameof(uri));

            #region retrieve content
            dataProviders ??= (r) => GetDefaultDataProcessors(r);
            if (queryParamsList?.Any() == true)
            {
                //dataModelContainer.Data = dataModelContainer.Data.GetDataModelParameters();
                // uri to use data model
                uri = new Uri(uri.AbsoluteUri
                    .Fill(queryParamsList)
                    , UriKind.Absolute);
            }
            if (!Uri.IsWellFormedUriString(uri.AbsoluteUri, UriKind.Absolute))
                throw new FormatException(
                    $"Invalid uri format : {uri.AbsoluteUri}");

            string content;
            
            if ((content = uri
                .GetAsync(token,
                referrer, userAgent)
                .GetAwaiter().GetResult()) == null)
                throw new TimeoutException(
                    $"Uri retrieval timed-out for {uri.AbsoluteUri}");

            #endregion
            
            if (string.IsNullOrEmpty(content)) return content;

            // var baseUri = new Uri(uri.GetLeftPart(UriPartial.Authority));

            var contentParentUri = uri.GetParentUri();

            contentParentUri??=new Uri(AppDomain.CurrentDomain.BaseDirectory);

            return content.RenderContent(
                queryParamsList,
                contentParentUri.AbsoluteUri,
                dataProviders,
                token,
                referrer,
                userAgent);

        }


        #endregion


        #region root implementations



        // root base implementation
        public static string? RenderContent(
            this string content,
            List<QueryParams>? queryParamsList = null,
            string? contentParentAbsolutePath = null,
            Func<TemplateMultiDataRequest, IEnumerable<dynamic>?>? dataProviders = null,
            CancellationToken? token = null,
            string? referrer = null,
            string? userAgent = null
        )
        {
            // todo: requires refactoring and optimizing

            // return content if it's empty
            if (string.IsNullOrWhiteSpace(content)) return content;

            // set default working directory to current app domain base directory
            if (string.IsNullOrWhiteSpace(contentParentAbsolutePath))
                contentParentAbsolutePath = AppDomain.CurrentDomain.BaseDirectory;

            var parentUri = new Uri(contentParentAbsolutePath);

            // set default data providers if none were provided
            dataProviders ??= (r) => GetDefaultDataProcessors(r);

            // check if content has date placeholders & fill them
            // keep date filling to be pre-rendered always
            // developers can use {now{HH:mm:ss}} and {tomorrow{dd MMM, yyyy}} and {yesterday{dd MMM, yyyy}}
            // placeholders carefully in their templates not to have a problem with timezones
            // and/or sql injection when used in conjunction with sql queries
            // todo: alternatively, consider adding a flag to disable date placeholders
            // or add fixed placeholders in the queryParameters taking into consideration
            // proper delimiters provided by the developer.
            // e.g. if the developer provides a delimiter of [ and ] then the placeholders will be [now[HH:mm:ss]] and [tomorrow[dd MMM, yyyy]] and [yesterday[dd MMM, yyyy]]
            content = content.FillDates();



            #region check for data providers and data tag availability in content

            IEnumerable<dynamic>? dataResponse = null;

            // newQueryParams is a list of query parameters that will be used to retrieve data from data providers
            // then the data from newQueryParams alongside the older data, coming from a parent nested call, will be used to fill the template
            // higher priority of which is given to the data from the child call (i.e. recent call)
            QueryParams newQueryParams = new();


            if (dataProviders != null)
            {

                var dataRequestMatch = Regex.Match(content,
                    DataTagContentRegex,
                    RegexOptions.Singleline);

                // get data if data request tags available
                if (dataRequestMatch.Success)
                {
                    // todo: move pre-render, connection-string to headers


                    // todo: refactor TemplateMultiDataRequest to remove ConnectionString, ContentType, and PreRender
                    // and have them read from attributes inside Com.H.Ef.Relationa.QueryExtensions.GetDefaultDataProcessors()

                    Dictionary<string, string?> attribs =
                        XElement.Parse(dataRequestMatch.Value)
                        .Attributes().ToDictionary(key => key.Name.LocalName, v => (string?) v.Value);
                        

                    // no pre-render data model before calling data providers
                    // (unless pre-render tag = true)
                    // as data model is submitted to data providers
                    // to allow data providers implement their own sql injection
                    // protection if needed

                    _ = bool.TryParse((dataRequestMatch
                        .GetAttrib("pre-render") 
                        ?? dataRequestMatch
                        .GetAttrib("pre_render") ?? "false"), out bool preRender);


                    // todo: refactor TemplateMultiDataRequest to remove ConnectionString, ContentType, and PreRender
                    // and have them read from attributes inside Com.H.Ef.Relationa.QueryExtensions.GetDefaultDataProcessors()
                    dataResponse = dataProviders(new()
                    {
                        QueryParamsList = queryParamsList,
                        Request = dataRequestMatch.Groups["content"]?.Value,
                        ConnectionString = 
                            dataRequestMatch?.GetAttrib("connection-string")
                            ?? dataRequestMatch?.GetAttrib("connection_string"),
                        ContentType = 
                            dataRequestMatch?.GetAttrib("content-type")
                            ?? dataRequestMatch?.GetAttrib("content_type"),
                        CancellationToken = token,
                        PreRender = preRender,
                        Attributes = attribs
                        
                    });

                    
                    if (dataRequestMatch?.GetAttrib("open-marker") != null
                        || dataRequestMatch?.GetAttrib("open_marker") != null
                        )
                        newQueryParams.OpenMarker = 
                            dataRequestMatch.GetAttrib("open-marker")
                            ?? dataRequestMatch.GetAttrib("open_marker");
                    
                    if (dataRequestMatch?.GetAttrib("close-marker") != null)
                        newQueryParams.CloseMarker = dataRequestMatch.GetAttrib("close-marker");
                    if (dataRequestMatch?.GetAttrib("null-value") != null)
                        newQueryParams.NullReplacement = dataRequestMatch.GetAttrib("null-value");
                }
            }


            #endregion


            // remove the data tag if it was available as it should 
            // already be processed by now and not needed anymore
            content = Regex.Replace(content, DataTagContentRegex,
                "", RegexOptions.Singleline);


            #region loop response data while rendering current recursive level content

            string renderedContent = "";
            if (dataResponse is not null)
            {
                if (queryParamsList is null) queryParamsList = new();
                foreach (var item in dataResponse.EnsureEnumerable())
                {
                    // todo: replace with markers from regex, failover to 
                    // subDataModelContainer markers
                    newQueryParams.DataModel = ((object)item)?.EnsureEnumerable().ToList();

                    // todo: seems there is a bug here where queryParamsList will keep growing
                    // on each iteration of the loop, keeping the old values from the previous iteration
                    // of the loop and adding the new values from the current iteration of the loop.
                    // this is not the expected behavior.
                    // either remove the last item from the list or before the end of the loop
                    // or use a new list for each iteration of the loop
                    queryParamsList?.Add(newQueryParams);


                    // content is the template with vars that gets filled with different
                    // data model and the fill result gets accumulated in filledContent
                    var filledContent = content.Fill(queryParamsList);

                    // retrieve sub-templates recursively from current recursive level 
                    // rendered content
                    foreach (var templateTagMatch in Regex.Matches(
                        filledContent, TemplateTagRegex).Cast<Match>())
                    {
                        filledContent = filledContent.RenderSubContent(
                            templateTagMatch,
                            parentUri,
                            queryParamsList,
                            dataProviders,
                            token,
                            referrer,
                            userAgent
                            );

                        #region replaced by refactored method
                        //var subUri = templateTagMatch.Groups["content"]?.Value;
                        //// fill placeholder for current uri
                        //if (subUri?.Contains("{uri{./}}") == true
                        //    || subUri?.Contains("{uri{.}}") == true
                        //    )
                        //    subUri = subUri
                        //        .Replace("{uri{./}}", parentUri.AbsoluteUri)
                        //        .Replace("{uri{.}}", parentUri.AbsoluteUri.RemoveLast(1));

                        //if (!string.IsNullOrWhiteSpace(subUri)
                        //    ||
                        //    Uri.IsWellFormedUriString(subUri, UriKind.Absolute)
                        //    )
                        //{
                        //    var subTemplateContent = new Uri(subUri).RenderContent(
                        //        queryParamsList,
                        //        dataProviders,
                        //        token,
                        //        // todo: optional grab of referrer from regex sub-template tag
                        //        referrer,
                        //        userAgent
                        //        );
                        //    // replace sub-template tag with rendered sub-template content
                        //    filledContent = filledContent.Replace(templateTagMatch.Value, subTemplateContent);
                        //}
                        //// sub-template tag removal if no valid uri was available
                        //else filledContent = filledContent.Replace(templateTagMatch.Value, "");
                        
                        #endregion
                    }
                    renderedContent += filledContent;
                    // to prevent the queryParamsList from growing indefinitely with each iteration of the loop
                    // remove the last item from the list as it's only needed during the current iteration.
                    queryParamsList?.Remove(newQueryParams);
                }

            }
            else
            {
                if (queryParamsList is not null && queryParamsList.Count>0) content = content.Fill(queryParamsList);
                foreach (var templateTagMatch in Regex.Matches(
                    content, TemplateTagRegex).Cast<Match>())
                {
                    content = content.RenderSubContent(
                        templateTagMatch,
                        parentUri,
                        queryParamsList,
                        dataProviders,
                        token,
                        referrer,
                        userAgent
                        );
                        
                    #region replaced by refactored method
                    //var subUri = templateTagMatch.Groups["content"]?.Value;
                    //// fill placeholder for current uri
                    //if (subUri?.Contains("{uri{./}}") == true
                    //    || subUri?.Contains("{uri{.}}") == true
                    //    )
                    //    subUri = subUri
                    //        .Replace("{uri{./}}", parentUri.AbsoluteUri)
                    //        .Replace("{uri{.}}", parentUri.AbsoluteUri.RemoveLast(1));

                    //if (!string.IsNullOrWhiteSpace(subUri)
                    //    ||
                    //    Uri.IsWellFormedUriString(subUri, UriKind.Absolute)
                    //    )
                    //{
                    //    var subTemplateContent = new Uri(subUri).RenderContent(
                    //        queryParamsList,
                    //        dataProviders,
                    //        token,
                    //        // todo: optional grab of referrer from regex sub-template tag
                    //        referrer,
                    //        userAgent
                    //        );
                    //    // replace sub-template tag with rendered sub-template content
                    //    content = content.Replace(templateTagMatch.Value, subTemplateContent);
                    //}
                    //// sub-template tag removal if no valid uri was available
                    //else content = content.Replace(templateTagMatch.Value, "");
                    #endregion
                }
                renderedContent = content;
            }
            #endregion


            return renderedContent;

        }

        private static string RenderSubContent(
            this string content,
            Match templateTagMatch,
            Uri parentUri,
            List<QueryParams>? queryParamsList,
            Func<TemplateMultiDataRequest, IEnumerable<dynamic>?>? dataProviders = null,
            CancellationToken? token = null,
            string? referrer = null,
            string? userAgent = null
            )
        {
            var subUri = templateTagMatch.Groups["content"]?.Value;
            // fill placeholder for current uri
            if (subUri?.Contains("{uri{./}}") == true
                || subUri?.Contains("{uri{.}}") == true
                )
                subUri = subUri
                    .Replace("{uri{./}}", parentUri.AbsoluteUri)
                    .Replace("{uri{.}}", parentUri.AbsoluteUri.RemoveLast(1));

            if (!string.IsNullOrWhiteSpace(subUri)
                ||
                Uri.IsWellFormedUriString(subUri, UriKind.Absolute)
                )
            {
                var subTemplateContent = new Uri(subUri).RenderContent(
                    queryParamsList,
                    dataProviders,
                    token,
                    // todo: optional grab of referrer from regex sub-template tag
                    referrer,
                    userAgent
                    );
                // replace sub-template tag with rendered sub-template content
                content = content.Replace(templateTagMatch.Value, subTemplateContent);
            }
            // sub-template tag removal if no valid uri was available
            else content = content.Replace(templateTagMatch.Value, "");
            return content;
        }



        #endregion

        #region creating default template data processor
        public static IEnumerable<dynamic>? GetDefaultDataProcessors(TemplateMultiDataRequest req)
        {
            var assemblyName = "Com.H.EF.Relational";

            Assembly? assembly = null;
            try
            {
                assembly = Com.H.Reflection.ReflectionExtensions.LoadAssembly(assemblyName);
            }
            catch { }


            if (assembly is null)
            {
                assemblyName = "Com.H.EF.Relational.dll";
                try
                {
                    assembly = Com.H.Reflection.ReflectionExtensions.LoadAssembly(assemblyName);
                }
                catch { }
            }

            if (assembly is null)
                // return null;
                throw new NotSupportedException("Couldn't find default NuGet package reference for Com.H.EF.Relational, "
                    + "nor assembly reference for Com.H.EF.Relational.dll. \r\n"
                    + "Please add a reference to either NuGet package Com.H.EF.Relational, or to assembly Com.H.EF.Relational.dll. \r\n"
                    + "Also, make sure to have either NuGet package Microsoft.EntityFrameworkCore.SqlServer "
                    + "or Microsoft.EntityFrameworkCore.Sqlite installed, depending on your database provider.");

            var classType = assembly.GetType("Com.H.EF.Relational.QueryExtensions");
            if (classType is null)
                //return null;
                throw new NotSupportedException("Couldn't find class Com.H.EF.Relational.QueryExtensions"
                    + " in assembly Com.H.EF.Relational");

            var method = classType.GetMethod("GetDefaultDataProcessors", new Type[] { typeof(TemplateMultiDataRequest) });
            if (method is null)
                //return null;
                throw new NotSupportedException("Couldn't find method GetDefaultDataProcessors in class Com.H.EF.Relational.QueryExtensions"
                    + " in assembly Com.H.EF.Relational");
            var args = new object[] { req };
            var result = method.Invoke(null, args);
            return (IEnumerable<dynamic>?)result;

        }


        
        #endregion


    }
}

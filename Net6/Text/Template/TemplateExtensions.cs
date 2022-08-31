using Com.H.Data;
using Com.H.Linq;
using Com.H.Net;
using Com.H.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;


namespace Com.H.Text.Template
{
    // note: fully working beta state (rough draft), requires heavy refactoring and optimization before final release
    public class TemplateDataRequest
    {
        public string? ConnectionString { get; set; }
        public string? ContentType { get; set; }
        public string? Request { get; set; }
        public dynamic? DataModel { get; set; }
        public CancellationToken? CancellationToken { get; set; }
        public string? OpenMarker { get; set; }
        public string? CloseMarker { get; set; }
        public string? NullReplacement { get; set; }
    }

    public class TemplateMultiDataRequest
    {
        public string? ConnectionString { get; set; }
        public string? ContentType { get; set; }
        public string? Request { get; set; }
        public bool PreRender { get; set; } = false;
        public IEnumerable<QueryParams>? QueryParamsList { get; set; }
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


        public static string TemplateTagRegex { get; set; } = @"\<(\s)*h\-embedded\-template(\s)*\>(\s)*\<\!\[CDATA\[(?<content>.*?)\]\]\>(\s)*\</h\-embedded\-template(\s)*\>";

        public static string? RenderContent(
             this string template,
             Encoding? encoding = null,
             dynamic? dataModel = null,
             string openingMarker = "{{",
             string closingMarker = "}}",
             string? nullReplacement = null,
             Func<TemplateDataRequest, IEnumerable<dynamic>>? dataProviders = null,
             CancellationToken? token = null,
             string? referrer = null,
             string? userAgent = null
             )
        {
            encoding ??= Encoding.UTF8;
            using var stream = new MemoryStream(encoding.GetBytes(template));
            return stream
                .RenderContent((object?)dataModel,
                openingMarker, closingMarker, nullReplacement,
                dataProviders, token, referrer, userAgent);
        }


        public static string? RenderContent(
         this Stream stream,
         dynamic? dataModel = null,
         string openingMarker = "{{",
         string closingMarker = "}}",
         string? nullReplacement = null,
         Func<TemplateDataRequest, IEnumerable<dynamic>>? dataProviders = null,
         CancellationToken? token = null,
         string? referrer = null,
         string? userAgent = null
         )
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var f = File.OpenWrite(tempFile))
                    if (token != null)
                        stream.CopyToAsync(f, (CancellationToken)token).GetAwaiter().GetResult();
                    else stream.CopyTo(f);
                return new Uri(tempFile)
                    .RenderContent((object?)dataModel, openingMarker, closingMarker,
                    nullReplacement, dataProviders, token, referrer, userAgent);
            }
            catch { throw; }
            finally
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch { }
            }
        }

        /// <summary>
        /// Renders nested text templates using one or more data providrs
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="dataModel"></param>
        /// <param name="openingMarker"></param>
        /// <param name="closingMarker"></param>
        /// <param name="nullReplacement"></param>
        /// <param name="dataProviders"></param>
        /// <param name="token"></param>
        /// <param name="referrer"></param>
        /// <param name="userAgent"></param>
        /// <returns></returns>
        public static string? RenderContent(
        this Uri uri,
        dynamic? dataModel = null,
        string openingMarker = "{{",
        string closingMarker = "}}",
        string? nullReplacement = null,
        Func<TemplateDataRequest, IEnumerable<dynamic>>? dataProviders = null,
        CancellationToken? token = null,
        string? referrer = null,
        string? userAgent = null
        )
        {
            return RenderContent(uri
                , new QueryParams
                {
                    DataModel = ((object?)dataModel)?.EnsureEnumerable(),
                    OpenMarker = openingMarker,
                    CloseMarker = closingMarker,
                    NullReplacement = nullReplacement
                },
                dataProviders,
                token,
                referrer,
                userAgent
                );
        }


        private static string FillDates(this string content)
            =>content.FillDate(DateTime.Now, "{now{")
                .FillDate(DateTime.Today.AddDays(1), "{tomorrow{");
        


        public static string? RenderContent(
        this Uri uri,
        QueryParams? dataModelContainer = null,
        Func<TemplateDataRequest, IEnumerable<dynamic>>? dataProviders = null,
        CancellationToken? token = null,
        string? referrer = null,
        string? userAgent = null
        )
        {
            // todo: needs heavy refactoring and optimizing
            #region retrieve content
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            if (dataModelContainer?.DataModel != null)
            {
                //dataModelContainer.Data = dataModelContainer.Data.GetDataModelParameters();
                // uri to use data model
                uri = new Uri(uri.AbsoluteUri
                    .Fill(dataModelContainer.DataModel,
                    dataModelContainer.OpenMarker,
                    dataModelContainer.CloseMarker,
                    dataModelContainer.NullReplacement)
                    .FillDates()
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

            if (content == null) return null;
            content = content.FillDates();
            #endregion



            #region check for data providers and data tag availability in content

            IEnumerable<dynamic>? dataResponse = null;
            var nextOpenMarker = dataModelContainer?.OpenMarker;
            var nextCloseMarker = dataModelContainer?.CloseMarker;
            var nextNullValue = dataModelContainer?.NullReplacement;

            if (dataProviders != null)
            {

                var dataRequestMatch = Regex.Match(content,
                    DataTagContentRegex,
                    RegexOptions.Singleline);

                // get data if data request tags available
                if (dataRequestMatch.Success)
                {
                    // no pre-filling data model before calling data providers
                    // as data model is submitted to data providers
                    // to allow data providers implement their own sql injection
                    // protection if needed

                    dataResponse = dataProviders(new()
                    {
                        Request = dataRequestMatch.Groups["content"]?.Value,
                        ConnectionString = dataRequestMatch
                        .GetAttrib("connection-string"),
                        ContentType = dataRequestMatch
                        .GetAttrib("content-type"),
                        CancellationToken = token,
                        DataModel = dataModelContainer?.DataModel?.GetDataModelParameters(),
                        OpenMarker = dataModelContainer?.OpenMarker,
                        CloseMarker = dataModelContainer?.CloseMarker
                    });

                    nextOpenMarker =
                        dataRequestMatch.GetAttrib("open-marker") ?? nextOpenMarker;
                    nextCloseMarker =
                       dataRequestMatch.GetAttrib("close-marker") ?? nextCloseMarker;
                    nextNullValue = dataRequestMatch.GetAttrib("null-value") ?? nextNullValue;

                }
            }


            #endregion


            #region loop response data while rendering current recursive level content



            // remove the data tag if it was available as it should 
            // already be processed by now and not needed anymore
            content = Regex.Replace(content, DataTagContentRegex,
                "", RegexOptions.Singleline);


            string renderedContent = "";
            if (dataResponse != null)
            {
                foreach (var item in dataResponse.EnsureEnumerable())
                {
                    // todo: replace with markers from regex, failover to 
                    // subDataModelContainer markers
                    QueryParams subDataModelContainer = new()
                    {
                        OpenMarker = nextOpenMarker,
                        CloseMarker = nextCloseMarker,
                        NullReplacement = nextNullValue,
                        DataModel = ((object)item)?.EnsureEnumerable()
                    };

                    if (dataModelContainer?.DataModel != null)
                        subDataModelContainer.DataModel
                            = (subDataModelContainer.DataModel as IEnumerable<object>)?
                            .Union(((object)dataModelContainer.DataModel)
                            .EnsureEnumerable());

                    // content is the template with vars that gets filled with different
                    // data model and the fill result gets accumulated in filledContent
                    var filledContent = content.Fill(
                        subDataModelContainer.DataModel,
                        subDataModelContainer.OpenMarker,
                        subDataModelContainer.CloseMarker,
                        subDataModelContainer.NullReplacement
                        );

                    // retrieve sub-templates recursively from current recursive level 
                    // rendered content
                    foreach (var templateTagMatch in Regex.Matches(
                        filledContent, TemplateTagRegex).Cast<Match>())
                    {
                        var subUri = templateTagMatch.Groups["content"]?.Value;
                        // todo: replace {uri{./}} placeholder with functioning uri traversal logic
                        if (subUri?.Contains("{uri{./}}") == true
                            || subUri?.Contains("{uri{.}}") == true
                            )
                            subUri = subUri
                                .Replace("{uri{./}}", uri?.GetParentUri()?.AbsoluteUri + "/")
                                .Replace("{uri{.}}", uri?.GetParentUri()?.AbsoluteUri.RemoveLast(1));

                        if (!string.IsNullOrWhiteSpace(subUri)
                            ||
                            Uri.IsWellFormedUriString(subUri, UriKind.Absolute)
                            )
                        {
                            var subTemplateContent = new Uri(subUri).RenderContent(
                                subDataModelContainer,
                                dataProviders,
                                token,
                                // todo: optional grab of referrer from regex sub-template tag
                                referrer,
                                userAgent
                                );
                            // replace sub-template tag with rendered sub-template content
                            filledContent = filledContent.Replace(templateTagMatch.Value, subTemplateContent);
                        }
                        // sub-template tag removal if no valid uri was available
                        else filledContent = filledContent.Replace(templateTagMatch.Value, "");
                    }
                    renderedContent += filledContent;
                }

            }
            else
            {
                if (dataModelContainer?.DataModel != null) content = content.Fill(
                    dataModelContainer.DataModel, dataModelContainer.OpenMarker,
                    dataModelContainer.CloseMarker, dataModelContainer.NullReplacement);
                foreach (var templateTagMatch in Regex.Matches(
                    content, TemplateTagRegex).Cast<Match>())
                {
                    var subUri = templateTagMatch.Groups["content"]?.Value;
                    // fill placeholder for current uri
                    if (subUri?.Contains("{uri{./}}") == true
                        || subUri?.Contains("{uri{.}}") == true
                        )
                        subUri = subUri
                            .Replace("{uri{./}}", uri?.GetParentUri()?.AbsoluteUri)
                            .Replace("{uri{.}}", uri?.GetParentUri()?.AbsoluteUri.RemoveLast(1));

                    if (!string.IsNullOrWhiteSpace(subUri)
                        ||
                        Uri.IsWellFormedUriString(subUri, UriKind.Absolute)
                        )
                    {
                        var subTemplateContent = new Uri(subUri).RenderContent(
                            dataModelContainer,
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
                }
                renderedContent = content;
            }
            #endregion


            return renderedContent;

        }


















        public static string? RenderContent(
        this Uri uri,
        List<QueryParams>? queryParamsList = null,
        Func<TemplateMultiDataRequest, IEnumerable<dynamic>?>? dataProviders = null,
        CancellationToken? token = null,
        string? referrer = null,
        string? userAgent = null
        )
        {
            // todo: needs heavy refactoring and optimizing
            #region retrieve content
            if (uri == null) throw new ArgumentNullException(nameof(uri));
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

            if (content == null) return null;
            content = content.FillDates();
            #endregion



            #region check for data providers and data tag availability in content

            IEnumerable<dynamic>? dataResponse = null;

            QueryParams newQueryParams = new();

            //var nextOpenMarker = queryParamsList?.OpenMarker;
            //var nextCloseMarker = queryParamsList?.CloseMarker;
            //var nextNullValue = queryParamsList?.NullReplacement;

            if (dataProviders != null)
            {

                var dataRequestMatch = Regex.Match(content,
                    DataTagContentRegex,
                    RegexOptions.Singleline);

                // get data if data request tags available
                if (dataRequestMatch.Success)
                {
                    // no pre-filling data model before calling data providers
                    // (unless pre-fill = true)
                    // as data model is submitted to data providers
                    // to allow data providers implement their own sql injection
                    // protection if needed


                    //TemplateMultiDataRequest req = new TemplateMultiDataRequest();
                    //req.QueryParamsList = queryParamsList;
                    //req.Request = dataRequestMatch.Groups["content"]?.Value;
                    //req.ConnectionString = dataRequestMatch?.GetAttrib("connection-string");
                    //req.ContentType = dataRequestMatch.GetAttrib("content-type");


                    _ = bool.TryParse((dataRequestMatch
                        .GetAttrib("pre-render") ?? "false"), out bool preRender);

                    dataResponse = dataProviders(new()
                    {
                        QueryParamsList = queryParamsList,
                        Request = dataRequestMatch.Groups["content"]?.Value,
                        ConnectionString = dataRequestMatch?
                        .GetAttrib("connection-string"),
                        ContentType = dataRequestMatch?
                        .GetAttrib("content-type"),
                        CancellationToken = token,
                        PreRender = preRender
                    });

                    //if (dataResponse !=null)
                    //    newQueryParams.DataModel = dataResponse;
                    if (dataRequestMatch?.GetAttrib("open-marker") != null)
                        newQueryParams.OpenMarker = dataRequestMatch.GetAttrib("open-marker");
                    if (dataRequestMatch?.GetAttrib("close-marker") != null)
                        newQueryParams.CloseMarker = dataRequestMatch.GetAttrib("close-marker");
                    if (dataRequestMatch?.GetAttrib("null-value") != null)
                        newQueryParams.NullReplacement = dataRequestMatch.GetAttrib("null-value");
                }
            }


            #endregion


            #region loop response data while rendering current recursive level content



            // remove the data tag if it was available as it should 
            // already be processed by now and not needed anymore
            content = Regex.Replace(content, DataTagContentRegex,
                "", RegexOptions.Singleline);


            string renderedContent = "";
            if (dataResponse != null)
            {
                foreach (var item in dataResponse.EnsureEnumerable())
                {
                    // todo: replace with markers from regex, failover to 
                    // subDataModelContainer markers
                    newQueryParams.DataModel = ((object)item)?.EnsureEnumerable();

                    queryParamsList?.Add(newQueryParams);


                    // content is the template with vars that gets filled with different
                    // data model and the fill result gets accumulated in filledContent
                    var filledContent = content.Fill(queryParamsList);

                    // retrieve sub-templates recursively from current recursive level 
                    // rendered content
                    foreach (var templateTagMatch in Regex.Matches(
                        filledContent, TemplateTagRegex).Cast<Match>())
                    {
                        var subUri = templateTagMatch.Groups["content"]?.Value;
                        // todo: replace {uri{./}} placeholder with functioning uri traversal logic
                        if (subUri?.Contains("{uri{./}}") == true
                            || subUri?.Contains("{uri{.}}") == true
                            )
                            subUri = subUri
                                .Replace("{uri{./}}", uri?.GetParentUri()?.AbsoluteUri + "/")
                                .Replace("{uri{.}}", uri?.GetParentUri()?.AbsoluteUri.RemoveLast(1));

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
                            filledContent = filledContent.Replace(templateTagMatch.Value, subTemplateContent);
                        }
                        // sub-template tag removal if no valid uri was available
                        else filledContent = filledContent.Replace(templateTagMatch.Value, "");
                    }
                    renderedContent += filledContent;
                }

            }
            else
            {
                if (queryParamsList != null) content = content.Fill(queryParamsList);
                foreach (var templateTagMatch in Regex.Matches(
                    content, TemplateTagRegex).Cast<Match>())
                {
                    var subUri = templateTagMatch.Groups["content"]?.Value;
                    // fill placeholder for current uri
                    if (subUri?.Contains("{uri{./}}") == true
                        || subUri?.Contains("{uri{.}}") == true
                        )
                        subUri = subUri
                            .Replace("{uri{./}}", uri?.GetParentUri()?.AbsoluteUri)
                            .Replace("{uri{.}}", uri?.GetParentUri()?.AbsoluteUri.RemoveLast(1));

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
                }
                renderedContent = content;
            }
            #endregion


            return renderedContent;

        }


    }
}

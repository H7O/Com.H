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
        public string ConnectionString { get; set; }
        public string ContentType { get; set; }
        public string Request { get; set; }
        public dynamic DataModel { get; set; }
        public CancellationToken? CancellationToken { get; set; }
    }

    public class TemplateDataResponse
    {
        public string OpeningMarker { get; set; }
        public string ClosingMarker { get; set; }
        public string NullReplacement { get; set; }
        public IEnumerable<dynamic> Data { get; set; }
        // todo: custom begin and end markers
    }
    public static class TemplateExtensions
    {
        // original without escape characters
        //    \<(\s)*h\-embedded\-data(\s)*((\s)+(connection(\-|_)string(\s)*=(\s)*"(?<c_str>.*?)")|(content(\-|_)type(\s)*=(\s)*"(?<c_type>.*?)"))?(\s)+((connection(\-|_)string(\s)*=(\s)*"(?<c_str>.*?)")|(content(\-|_)type(\s)*=(\s)*"(?<c_type>.*?)"))?\>(\s)*\<\!\[CDATA\[(?<content>.*?)\]\]\>(\s)*\</h\-embedded\-data(\s)*\>
        public static string DataTagRegex { get; set; } = @"\<(\s)*h\-embedded\-data(\s)*((\s)+(connection(\-|_)string(\s)*=(\s)*""(?<c_str>.*?)"")|(content(\-|_)type(\s)*=(\s)*""(?<c_type>.*?)""))?(\s)+((connection(\-|_)string(\s)*=(\s)*""(?<c_str>.*?)"")|(content(\-|_)type(\s)*=(\s)*""(?<c_type>.*?)""))?\>(\s)*\<\!\[CDATA\[(?<content>.*?)\]\]\>(\s)*\</h\-embedded\-data(\s)*\>";

        public static string TemplateTagRegex { get; set; } = @"\<(\s)*h\-embedded\-template(\s)*\>(\s)*\<\!\[CDATA\[(?<content>.*?)\]\]\>(\s)*\</h\-embedded\-template(\s)*\>";

        public static string RenderContent(
             this string template,
             Encoding encoding = null,
             dynamic dataModel = null,
             string openingMarker = "{{",
             string closingMarker = "}}",
             string nullReplacement = null,
             Func<TemplateDataRequest, TemplateDataResponse> dataProviders = null,
             CancellationToken? token = null,
             string referrer = null,
             string userAgent = null
             )
        {
            encoding ??= Encoding.UTF8;
            using var stream = new MemoryStream(encoding.GetBytes(template));
            return stream
                .RenderContent((object)dataModel,
                openingMarker, closingMarker, nullReplacement,
                dataProviders, token, referrer, userAgent);
        }


        public static string RenderContent(
         this Stream stream,
         dynamic dataModel = null,
         string openingMarker = "{{",
         string closingMarker = "}}",
         string nullReplacement = null,
         Func<TemplateDataRequest, TemplateDataResponse> dataProviders = null,
         CancellationToken? token = null,
         string referrer = null,
         string userAgent = null
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
                    .RenderContent((object)dataModel, openingMarker, closingMarker,
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
        public static string RenderContent(
        this Uri uri,
        dynamic dataModel = null,
        string openingMarker = "{{",
        string closingMarker = "}}",
        string nullReplacement = null,
        Func<TemplateDataRequest, TemplateDataResponse> dataProviders = null,
        CancellationToken? token = null,
        string referrer = null,
        string userAgent = null
        )
        {
            // todo: needs heavy refactoring and optimizing
            #region retrieve content
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            if (dataModel != null)
            {
                // uri to use data model
                uri = new Uri(uri.AbsoluteUri
                    .Fill((object)dataModel,
                    openingMarker,
                    closingMarker,
                    nullReplacement)
                    , UriKind.Absolute);
            }
            if (!Uri.IsWellFormedUriString(uri.AbsoluteUri, UriKind.Absolute))
                throw new FormatException(
                    $"Invalid uri format : {uri.AbsoluteUri}");

            string content = null;
            if ((content = uri
                .GetContentAsync(token,
                referrer, userAgent)
                .GetAwaiter().GetResult()) == null)
                throw new TimeoutException(
                    $"Uri retrieval timed-out for {uri.AbsoluteUri}");

            if (content == null) return null;

            #endregion



            #region check for data providers and data tag availability in content

            TemplateDataResponse dataResponse = null;

            if (dataProviders != null)
            {

                var dataRequestMatch = Regex.Match(content,
                    DataTagRegex,
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
                        .Groups["c_str"]?.Value,
                        ContentType = dataRequestMatch
                        .Groups["c_type"]?.Value,
                        CancellationToken = token,
                        DataModel = dataModel

                    });

                }
            }


            #endregion


            #region loop response data while rendering current recursive level content




            // remove the data tag if it was available as it should 
            // already be processed by now and not needed anymore
            content = Regex.Replace(content, DataTagRegex,
                "", RegexOptions.Singleline);


            // fill original (method caller not data provider caller) 
            // data model along with original markers
            //if (dataModel != null) content = content.Fill(
            //    (object) dataModel, openingMarker, 
            //    closingMarker, nullReplacement);

            //// ensure there is a response
            //if (dataResponse == null) dataResponse = new TemplateDataResponse();
            //if (dataResponse.Data == null) dataResponse.Data = ((object)dataModel)?.EnsureEnumerable();


            string renderedContent = "";
            if (dataResponse?.Data != null)
            {
                foreach (var item in dataResponse?.Data?.EnsureEnumerable())
                {
                    IEnumerable<dynamic> subDataModel =
                        ((object)item).EnsureEnumerable();
                    if (dataModel != null)
                        subDataModel = subDataModel
                        .Union(((object)dataModel).EnsureEnumerable());

                    // content is the template with vars that gets filled with different
                    // data model and the fill result gets accumulated in filledContent
                    var filledContent = content.Fill(
                        subDataModel,
                        dataResponse.OpeningMarker ?? openingMarker,
                        dataResponse.ClosingMarker ?? closingMarker,
                        dataResponse.NullReplacement ?? nullReplacement
                        );

                    // retrieve sub-templates recursively from current recursive level 
                    // rendered content
                    foreach (var templateTagMatch in Regex.Matches(
                        filledContent, TemplateTagRegex).Cast<Match>())
                    {
                        var subUri = templateTagMatch.Groups["content"]?.Value;
                        if (subUri?.Contains("{uri{current}}") == true)
                            subUri = subUri.Replace("{uri{current}}", uri.GetParentUri().AbsoluteUri);

                        if (!string.IsNullOrWhiteSpace(subUri)
                            ||
                            Uri.IsWellFormedUriString(subUri, UriKind.Absolute)
                            )
                        {
                            //IEnumerable<dynamic> subDataModel =
                            //    ((object)item).EnsureEnumerable();
                            //if (dataModel!=null)
                            //        subDataModel
                            //        .Union(((object) dataModel).EnsureEnumerable());
                            var subTemplateContent = new Uri(subUri).RenderContent(
                                (object)subDataModel,
                                dataResponse.OpeningMarker ?? openingMarker,
                                dataResponse.ClosingMarker ?? closingMarker,
                                dataResponse.NullReplacement ?? nullReplacement,
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
                if (dataModel != null) content = content.Fill(
                    (object)dataModel, openingMarker,
                    closingMarker, nullReplacement);
                foreach (var templateTagMatch in Regex.Matches(
                    content, TemplateTagRegex).Cast<Match>())
                {
                    var subUri = templateTagMatch.Groups["content"]?.Value;
                    // fill placeholder for current uri
                    if (subUri?.Contains("{uri{current}}") == true)
                        subUri = subUri.Replace("{uri{current}}", uri.GetParentUri().AbsoluteUri);
                    if (!string.IsNullOrWhiteSpace(subUri)
                        ||
                        Uri.IsWellFormedUriString(subUri, UriKind.Absolute)
                        )
                    {
                        var subTemplateContent = new Uri(subUri).RenderContent(
                            (object)dataModel,
                            openingMarker,
                            closingMarker,
                            nullReplacement,
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

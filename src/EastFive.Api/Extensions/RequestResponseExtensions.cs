using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Collections.Generic;
using EastFive.Api.Resources;
using System.Security.Claims;
using System.Configuration;
using EastFive.Api;
using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Web;
using EastFive.Linq.Async;
using EastFive.Web.Configuration;
using Microsoft.AspNetCore.Http;
using EastFive.Api.Core;
using System.Drawing.Imaging;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq.Expressions;

namespace EastFive.Api
{
    public static class RequestResponseExtensions
    {
        public static IHttpResponse CreateResponse(this IHttpRequest request, HttpStatusCode statusCode)
        {
            return new HttpResponse(request, statusCode);
        }

        public static IHttpResponse CreateResponse<T>(this IHttpRequest request,
            HttpStatusCode statusCode,
            T content)
        {
            return new JsonHttpResponse(request, statusCode, content);
        }

        public static IHttpResponse CreateExtrudedResponse(this IHttpRequest request,
            HttpStatusCode statusCode, object[] contents)
        {
            throw new NotImplementedException();
        }

        private static IHttpResponse CreateHttpMultipartResponse(this IHttpRequest request,
            IEnumerable<IHttpRequest> contents)
        {
            var multipartContent = new MultipartContent("mixed", "----Boundary_" + Guid.NewGuid().ToString("N"));
            request.CreateResponse(HttpStatusCode.OK, multipartContent);
            throw new NotImplementedException();
            //foreach (var content in contents)
            //{
            //    multipartContent.Add(new HttpMessageContent(content));
            //}
            //var response = request.CreateResponse(HttpStatusCode.OK);
            //response.Content = multipartContent;
            //return response;
        }

        public static IHttpResponse CreateFileResponse(this IHttpRequest request, byte[] content, string mediaType,
            bool? inline = default(bool?), string filename = default(string))
        {
            var response = new BytesHttpResponse(request, HttpStatusCode.OK, filename, mediaType, inline, content);
            return response;
            //response.Content = new ByteArrayContent(content);
            //response.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            //if (inline.HasValue)
            //    response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue(inline.Value ? "inline" : "attachment")
            //    {
            //        FileName =
            //                default(string) == filename ?
            //                    Guid.NewGuid().ToString("N") + ".pdf" :
            //                    filename,
            //    };
            //return response;
        }

        #region ASDF


        public static HttpResponseMessage CreateContentResponse(this HttpRequestMessage request, byte[] content, string mediaType)
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Content = new ByteArrayContent(content);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            return response;
        }

        public static HttpResponseMessage CreateFileResponse(this HttpRequestMessage request, byte[] content, string mediaType,
            bool? inline = default(bool?), string filename = default(string))
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Content = new ByteArrayContent(content);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            if (inline.HasValue)
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue(inline.Value ? "inline" : "attachment")
                {
                    FileName =
                            default(string) == filename ?
                                Guid.NewGuid().ToString("N") + ".pdf" :
                                filename,
                };
            return response;
        }

        public static HttpResponseMessage CreatePdfResponse(this HttpRequestMessage request, System.IO.Stream stream,
            string filename = default(string), bool inline = false)
        {
            var pdfData = stream.ToBytes();
            var result = request.CreatePdfResponse(pdfData, filename, inline);
            return result;
        }

        public static HttpResponseMessage CreatePdfResponse(this HttpRequestMessage request, byte[] pdfData,
            string filename = default(string), bool inline = false)
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Content = new ByteArrayContent(pdfData);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment")
            {
                FileName =
                            default(string) == filename ?
                                Guid.NewGuid().ToString("N") + ".pdf" :
                                filename,
            };
            return response;
        }

        public static HttpResponseMessage CreateImageResponse(this HttpRequestMessage request, byte[] imageData,
            int? width = default(int?), int? height = default(int?), bool? fill = default(bool?),
            string filename = default(string), string contentType = default(string))
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                throw new NotSupportedException("OS not supported");

            if (width.HasValue || height.HasValue || fill.HasValue)
            {
                var image = System.Drawing.Image.FromStream(new MemoryStream(imageData));
                return request.CreateImageResponse(image, width, height, fill, filename);
            }
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Content = new ByteArrayContent(imageData);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(String.IsNullOrWhiteSpace(contentType) ? "image/png" : contentType);
            return response;
        }

        public static HttpResponseMessage CreateImageResponse(this HttpRequestMessage request, Image image,
            int? width = default(int?), int? height = default(int?), bool? fill = default(bool?),
            string filename = default(string))
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                throw new NotSupportedException("OS not supported");

            var response = request.CreateResponse(HttpStatusCode.OK);
            var ratio = ((double)image.Size.Width) / ((double)image.Size.Height);
            var newWidth = (int)Math.Round(width.HasValue ?
                    width.Value
                    :
                    height.HasValue ?
                        height.Value * ratio
                        :
                        image.Size.Width);
            var newHeight = (int)Math.Round(height.HasValue ?
                    height.Value
                    :
                    width.HasValue ?
                        width.Value / ratio
                        :
                        image.Size.Width);

            var newImage = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);

            //set the new resolution
            newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //start the resizing
            using (var graphics = System.Drawing.Graphics.FromImage(newImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                var brush = System.Drawing.Brushes.White;
                graphics.FillRectangle(brush, 0, 0, newWidth, newHeight);

                //set some encoding specs
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
            }

            var encoder = getEncoderInfo("image/jpeg");
            #pragma warning disable CA1416
            response.Content = new PushStreamContent(
                (outputStream, httpContent, transportContext) =>
                {
                    var encoderParameters = new EncoderParameters(1);
                    encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 80L);

                    newImage.Save(outputStream, encoder, encoderParameters);
                    outputStream.Close();
                }, new MediaTypeHeaderValue(encoder.MimeType));
            #pragma warning restore CA1416
            return response;
        }

        public static HttpResponseMessage CreateImageResponse(this HttpRequestMessage request,
            Image image,
            string filename = default(string),
            string contentType = "image/jpeg")
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                throw new NotSupportedException("OS not supported");

            var response = request.CreateResponse(HttpStatusCode.OK);

            var encoder = getEncoderInfo(contentType);
            #pragma warning disable CA1416
            response.Content = new PushStreamContent(
                (outputStream, httpContent, transportContext) =>
                {
                    var encoderParameters = new EncoderParameters(1);
                    encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 80L);

                    image.Save(outputStream, encoder, encoderParameters);
                    outputStream.Close();
                }, new MediaTypeHeaderValue(encoder.MimeType));
            #pragma warning restore CA1416
            //TODO: response.Content.Headers. = filename
            return response;
        }

        private static ImageCodecInfo getEncoderInfo(string mimeType)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                throw new NotSupportedException("OS not supported");

            ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();

            for (int j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType.ToLower() == mimeType.ToLower())
                {
                    return encoders[j];
                }
            }

            return null;
        }

        public static HttpResponseMessage CreateResponseVideoStream(this HttpRequestMessage request,
            byte[] video, string contentType)
        {
            var response = request.CreateResponse(HttpStatusCode.PartialContent);
            var ranges =
                (
                    request.Headers.Range.IsDefaultOrNull() ?
                        default(RangeItemHeaderValue[])
                        :
                        request.Headers.Range.Ranges
                )
                .NullToEmpty();
            if (!ranges.Any())
                ranges = new RangeItemHeaderValue[]
                    {
                        new RangeItemHeaderValue(0, video.LongLength-1)
                    };

            response.Content = new PushStreamContent(
                async (outputStream, httpContent, transportContext) =>
                {
                    try
                    {
                        foreach (var range in ranges)
                        {
                            if (!range.From.HasValue)
                                continue;
                            var length = range.To.HasValue ?
                                (range.To.Value - range.From.Value)
                                :
                                (video.LongLength - range.From.Value);
                            await outputStream.WriteAsync(video, (int)range.From.Value, (int)length);
                        }
                    }
                    catch (Exception)
                    {
                        return;
                    }
                    finally
                    {
                        outputStream.Close();
                    }
                }, new MediaTypeHeaderValue(contentType));
            response.Headers.AcceptRanges.Add("bytes");
            response.Headers.CacheControl = new CacheControlHeaderValue() { MaxAge = TimeSpan.FromSeconds(10368000), };
            response.Content.Headers.ContentLength = video.LongLength;
            var rangeFirst = ranges.First();
            var to = rangeFirst.To.HasValue ?
                rangeFirst.To.Value
                :
                video.LongLength;
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(rangeFirst.From.Value, to, video.Length);
            return response;
        }

        public static HttpResponseMessage CreateHtmlResponse(this HttpRequestMessage request, string html)
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(html);
            writer.Flush();
            stream.Position = 0;
            response.Content = new StreamContent(stream);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return response;
        }

        //public static HttpResponseMessage CreateRedirectResponse<TController>(this IHttpRequest request, UrlHelper url,
        //    string routeName = null)
        //{
        //    var location = url.GetLocation<TController>(routeName);
        //    return request.CreateRedirectResponse(location);
        //}

        public static IHttpResponse CreateRedirectResponse(this IHttpRequest request, Uri location,
            string routeName = null)
        {
            var response = request.CreateResponse(HttpStatusCode.Redirect);
            response.SetLocation(location);
            return response;
        }

        //public static HttpResponseMessage CreateResponseSeeOther<TController>(this HttpRequestMessage request, Guid otherResourceId, UrlHelper url,
        //    string routeName = null)
        //{
        //    var location = url.GetLocation<TController>(otherResourceId, routeName);
        //    var response = request
        //                .CreateResponse(HttpStatusCode.SeeOther);
        //    response.Headers.Location = location;
        //    return response;
        //}

        //public static HttpResponseMessage CreateAlreadyExistsResponse<TController>(this HttpRequestMessage request, Guid existingResourceId, UrlHelper url,
        //    string routeName = null)
        //{
        //    var location = url.GetLocation<TController>(existingResourceId, routeName);
        //    var reason = $"There is already a resource with ID = [{existingResourceId}]";
        //    var response = request
        //                .CreateResponse(HttpStatusCode.Conflict)
        //                .AddReason(reason);
        //    response.Headers.Location = location;
        //    return response;
        //}

        //public static HttpResponseMessage CreateAlreadyExistsResponse(this HttpRequestMessage request, Type controllerType, Guid existingResourceId, UrlHelper url,
        //    string routeName = null)
        //{
        //    var location = url.GetLocation(controllerType, existingResourceId, routeName);
        //    var reason = $"There is already a resource with ID = [{existingResourceId}]";
        //    var response = request
        //                .CreateResponse(HttpStatusCode.Conflict)
        //                .AddReason(reason);
        //    response.Headers.Location = location;
        //    return response;
        //}

        public static IHttpResponse CreateResponseNotFound(this IHttpRequest request, Guid resourceId)
        {
            var reason = $"The resource with ID = [{resourceId}] was not found";
            var response = request
                .CreateResponse(HttpStatusCode.NotFound)
                .AddReason(reason);
            return response;
        }

        public static IHttpResponse CreateResponseConfiguration(this IHttpRequest request, string configParameterName, string why)
        {
            var response = request
                .CreateResponse(HttpStatusCode.ServiceUnavailable)
                .AddReason(why);
            return response;
        }

        public static IHttpResponse CreateResponseUnexpectedFailure(this IHttpRequest request, string why)
        {
            var response = request
                .CreateResponse(HttpStatusCode.InternalServerError)
                .AddReason(why);
            return response;
        }

        public static IHttpResponse CreateResponseEmptyId<TQuery, TProperty>(this IHttpRequest request,
            TQuery query, Expression<Func<TQuery, TProperty>> propertyFailing)
        {
            var value = string.Empty;
            var reason = $"Property [{propertyFailing}] must have value.";
            var response = request
                .CreateResponse(HttpStatusCode.BadRequest)
                .AddReason(reason);
            return response;
        }

        public static IHttpResponse CreateResponseValidationFailure<TQuery, TProperty>(this IHttpRequest request,
            TQuery query, Expression<Func<TQuery, TProperty>> propertyFailing)
        {
            var value = string.Empty;
            try
            {
                value = propertyFailing.Compile().Invoke(query).ToString();
            }
            catch (Exception)
            {

            }
            var reason = $"Property [{propertyFailing}] Value = [{value}] is not valid";
            var response = request
                .CreateResponse(HttpStatusCode.BadRequest)
                .AddReason(reason);
            return response;
        }

        /// <summary>
        /// The resource could not be created or updated due to a link to a resource that no longer exists.
        /// </summary>
        /// <typeparam name="TController"></typeparam>
        /// <param name="request"></param>
        /// <param name="brokenResourceId"></param>
        /// <param name="url"></param>
        /// <param name="routeName"></param>
        /// <returns></returns>
        //public static HttpResponseMessage CreateBrokenReferenceResponse<TController>(this HttpRequestMessage request,
        //    Guid? brokenResourceId, UrlHelper url,
        //    string routeName = null)
        //{
        //    var reference = url.GetWebId<TController>(brokenResourceId, routeName);
        //    var reason = $"The resource with ID = [{brokenResourceId}] at [{reference.Source}] is not available";
        //    var response = request
        //                .CreateResponse(HttpStatusCode.Conflict, reference)
        //                .AddReason(reason);
        //    return response;
        //}

        /// <summary>
        /// A query parameter makes reference to a secondary resource that does not exist.
        /// </summary>
        /// <example>/Foo?bar=ABC where the resource referenced by parameter bar does not exist</example>
        /// <typeparam name="TController">The Controller that represents the parameter linked resource</typeparam>
        /// <param name="request"></param>
        /// <param name="brokenResourceProperty"></param>
        /// <param name="url"></param>
        /// <param name="routeName"></param>
        /// <returns></returns>
        //public static HttpResponseMessage[] CreateLinkedDocumentNotFoundResponse<TController, TQueryResource>(this HttpRequestMessage request,
        //    TQueryResource query,
        //    Expression<Func<TQueryResource, WebIdQuery>> brokenResourceProperty,
        //    UrlHelper url,
        //    string routeName = null)
        //{
        //    var reference = default(WebIdQuery);
        //    try
        //    {
        //        reference = brokenResourceProperty.Compile().Invoke(query);
        //    } catch(Exception)
        //    {

        //    }

        //    var reason = reference.IsDefault()?
        //        $"The referenced [{typeof(TController).Name}] resource is not found"
        //        :
        //        $"The resource with ID = [{reference.UUIDs}] at [{reference.Source}] is not available";
        //    var response = request
        //                .CreateResponse(HttpStatusCode.Conflict, reference)
        //                .AddReason(reason);
        //    return response.AsEnumerable().ToArray();
        //}

        //public static HttpResponseMessage[] CreateLinkedDocumentNotFoundResponse<TController, TQueryResource>(this HttpRequestMessage request,
        //    Guid value,
        //    Expression<Func<TQueryResource, WebIdQuery>> brokenResourceProperty,
        //    UrlHelper url,
        //    string routeName = null)
        //{
        //    var reason = brokenResourceProperty.PropertyName(
        //        propertyName => $"The referenced [{typeof(TController).Name}] resource [Property({propertyName}) = {value}] is not found",
        //        () => $"The referenced {typeof(TController).Name} resource = [{value}] is not found");
        //    var response = request
        //                .CreateResponse(HttpStatusCode.Conflict)
        //                .AddReason(reason);
        //    return response.AsEnumerable().ToArray();
        //}


        #endregion

    }
}

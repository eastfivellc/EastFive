using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Web;

using Microsoft.CodeAnalysis;
using Microsoft.AspNetCore.Razor.Hosting;

using SixLabors.ImageSharp.Formats;

using EastFive;
using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Images;
using EastFive.Serialization;
using EastFive.Api.Resources;
using System.Threading;

namespace EastFive.Api
{
    #region Objects

    [BodyTypeResponse(StatusCode = HttpStatusCode.OK)]
    public delegate IHttpResponse ContentTypeResponse<TResource>(TResource content, string contentType = default(string));

    [BodyResponse(StatusCode = System.Net.HttpStatusCode.OK)]
    public delegate IHttpResponse ContentResponse(object content, string contentType = default(string));

    #endregion

    #region Data

    [BytesResponse]
    public delegate IHttpResponse BytesResponse(byte[] bytes, string filename = default, string contentType = default, bool? inline = default);
    public class BytesResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Raw data (byte [])";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            BytesResponse responseDelegate = (bytes, filename, contentType, inline) =>
            {
                var response = new BytesHttpResponse(request, this.StatusCode,
                    fileName: filename, contentType: contentType, inline: inline,
                    bytes);

                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess(responseDelegate);
        }
    }

    [StreamResponse]
    public delegate IHttpResponse StreamResponse(Stream stream, string filename = default, string contentType = default, bool? inline = default);
    public class StreamResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Raw data (Stream)";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            StreamResponse responseDelegate = (stream, filename, contentType, inline) =>
            {
                var response = new StreamHttpResponse(request, this.StatusCode,
                    fileName: filename, contentType: contentType, inline: inline,
                    stream);

                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess(responseDelegate);
        }
    }

    [WriteStreamResponse]
    public delegate IHttpResponse WriteStreamResponse(Action<Stream> streamWriter, string filename = default, string contentType = default, bool? inline = default);
    public class WriteStreamResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Raw data (Stream)";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            WriteStreamResponse responseDelegate = (streamWriter, filename, contentType, inline) =>
            {
                var response = new WriteStreamHttpResponse(request, this.StatusCode,
                    fileName: filename, contentType: contentType, inline: inline,
                    streamWriter);

                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess(responseDelegate);
        }
    }

    [WriteStreamAsyncResponse]
    public delegate IHttpResponse WriteStreamAsyncResponse(Func<Stream, Task> streamWriter, string filename = default, string contentType = default, bool? inline = default);
    public class WriteStreamAsyncResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Raw data (Stream)";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            WriteStreamAsyncResponse responseDelegate = (streamWriter, filename, contentType, inline) =>
            {
                var response = new WriteStreamAsyncHttpResponse(request, this.StatusCode,
                    fileName: filename, contentType: contentType, inline: inline,
                    streamWriter);

                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess(responseDelegate);
        }
    }

    [WriteStreamSyncAsyncResponse]
    public delegate IHttpResponse WriteStreamSyncAsyncResponse(
        Func<Stream, Task> streamWriter,
        string filename = default, string contentType = default, 
        bool? inline = default);
    public class WriteStreamSyncAsyncResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Raw data (Stream)";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            WriteStreamSyncAsyncResponse responseDelegate = (streamWriter, filename, contentType, inline) =>
            {
                var response = new WriteStreamSyncAsyncHttpResponse(request, this.StatusCode,
                    fileName: filename, contentType: contentType, inline: inline,
                    streamWriter);

                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess(responseDelegate);
        }
    }

    [TextResponse]
    public delegate IHttpResponse TextResponse(string content, Encoding encoding = default,
        string filename = default, string contentType = default, bool? inline = default);
    public class TextResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Text";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            TextResponse responseDelegate = (content, encoding, filename, contentType, inline) =>
            {
                var response = new StringHttpResponse(request, this.StatusCode,
                    fileName:filename, contentType:contentType, inline:inline,
                    content, encoding);

                return UpdateResponse(parameterInfo, httpApp, request, response);

            };
            return onSuccess(responseDelegate);
        }
    }

    #region Images

    [ImageRawResponse]
    public delegate IHttpResponse ImageRawResponse(byte[] bytes,
        int? width = default(int?), int? height = default(int?), bool? fill = default(bool?),
        string filename = default, string contentType = default);
    public class ImageRawResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Raw data (byte [])";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            ImageRawResponse responseDelegate = (imageData, width, height, fill,
                            filename, contentType) =>
            {
                if (width.HasValue || height.HasValue || fill.HasValue)
                {
                    //try
                    //{
                    //    var image = System.Drawing.Image.FromStream(new MemoryStream(imageData));
                    //    var resizedResponse = new ImageHttpResponse(request, this.StatusCode,
                    //        image, width, height, fill, filename);
                    //    return UpdateResponse(parameterInfo, httpApp, request, resizedResponse);
                    //} catch(TypeInitializationException) 
                    //{
                        // Was not Windoze

                        if (imageData.TryReadImage(out SixLabors.ImageSharp.Image image, out IImageFormat format))
                        {
                            var resizedResponse = new ImageSharpHttpResponse(request, this.StatusCode,
                                image, width, height, fill, filename);
                            return UpdateResponse(parameterInfo, httpApp, request, resizedResponse);
                        }
                    //}
                }
                var contentTypeFinal = GetContentType();
                var response = new BytesHttpResponse(request, this.StatusCode,
                    filename,
                    contentTypeFinal, 
                    default,
                    imageData);
                //response.SetContentType(contentTypeFinal);

                return UpdateResponse(parameterInfo, httpApp, request, response);

                string GetContentType()
                {
                    if (contentType.NullToEmpty().StartsWith("image", StringComparison.OrdinalIgnoreCase))
                        return contentType;

                    if (imageData.TryReadImage(out SixLabors.ImageSharp.Image image, out IImageFormat format))
                        return format.DefaultMimeType;

                    return contentType;
                }
            };
            return onSuccess((object)responseDelegate);
        }

    }


    [ImageResponse]
    public delegate IHttpResponse ImageResponse(Image image,
        int? width = default, int? height = default, bool? fill = default,
        Brush background = default,
        string contentType = default,
        string filename = default);
    public class ImageResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Raw data (byte [])";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                throw new NotSupportedException("OS not supported");

            #pragma warning disable CA1416
            ImageResponse responseDelegate = (image, 
                width, height, fill, background,
                contentType, filename) =>
            {
                var newImage = image.ResizeImage(width, height, fill, background);
                var codec = contentType.ParseImageCodecInfo();
                var response = new ImageHttpResponse(newImage, codec, request, this.StatusCode);
                response.SetContentType(codec.MimeType);
                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            #pragma warning restore CA1416
            return onSuccess((object)responseDelegate);
        }

        private class ImageHttpResponse : HttpResponse
        {
            public ImageHttpResponse(Image image, ImageCodecInfo codec,
                IHttpRequest request, HttpStatusCode statusCode)
                : base(request, statusCode,
                      async (responseStream) =>
                      {
                          if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                              throw new NotSupportedException("OS not supported");

                          // Hack for bug with images
                          using (var intermediaryStream = new MemoryStream())
                          {
                              image.Save(intermediaryStream, codec,
                                  encoderQuality: 80L);
                              intermediaryStream.Position = 0;
                              await intermediaryStream.CopyToAsync(responseStream);
                              await intermediaryStream.FlushAsync();
                              await responseStream.FlushAsync();
                              //responseStream.WriteAsync(intermediaryStream.ToArray())
                          }
                      })
            {
            }
        }
    }

    [ImageDisposableResponse]
    public delegate IHttpResponse ImageDisposableResponse(Image image,
        int? width = default, int? height = default, bool? fill = default,
        Brush background = default,
        string contentType = default,
        string filename = default);
    public class ImageDisposableResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Raw data (byte [])";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                throw new NotSupportedException("OS not supported");

            #pragma warning disable CA1416
            ImageDisposableResponse responseDelegate = (image,
                width, height, fill, background,
                contentType, filename) =>
            {
                image.FixOrientation();
                var newImage = image.ResizeImage(width, height, fill, background);
                var codec = contentType.ParseImageCodecInfo();
                var imageStream = new MemoryStream();
                newImage.Save(imageStream, codec,
                        encoderQuality: 80L);
                imageStream.Position = 0;
                var response = new ImageDisposableHttpResponse(imageStream, codec, request, this.StatusCode);
                response.SetContentType(codec.MimeType);
                if (newImage != image)
                    newImage.Dispose();
                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            #pragma warning restore CA1416
            return onSuccess((object)responseDelegate);
        }

        private class ImageDisposableHttpResponse : HttpResponse
        {
            public ImageDisposableHttpResponse(MemoryStream imageStream, ImageCodecInfo codec,
                IHttpRequest request, HttpStatusCode statusCode)
                : base(request, statusCode,
                      async (responseStream) =>
                      {
                          await imageStream.CopyToAsync(responseStream);
                          await imageStream.FlushAsync();
                          await responseStream.FlushAsync();
                          await imageStream.DisposeAsync();
                      })
            {
            }
        }
    }

    [ImageSharpResponse]
    public delegate IHttpResponse ImageSharpResponse(SixLabors.ImageSharp.Image image,
        int? width = default, int? height = default,
        string contentType = default,
        string filename = default);
    public class ImageSharpResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Raw data (byte [])";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            ImageSharpResponse responseDelegate = (image,
                width, height, 
                contentType, filename) =>
            {
                var response = new ImageSharpHttpResponse(image,
                    width, height, contentType,
                    request, this.StatusCode);
                response.SetContentType(contentType);
                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess((object)responseDelegate);
        }

        private class ImageSharpHttpResponse : EastFive.Api.HttpResponse
        {
            private SixLabors.ImageSharp.Image image;
            private int? width;
            private int? height;
            private string contentType;

            public ImageSharpHttpResponse(SixLabors.ImageSharp.Image image,
                    int? width, int? height, string contentType,
                    IHttpRequest request, HttpStatusCode statusCode)
                : base(request, statusCode)
            {
                this.image = image;
                this.width = width;
                this.height = height;
                this.contentType = contentType;
            }

            public override async Task WriteResponseAsync(Stream responseStream)
            {
                var newImage = image.ResizeImage(width, height);
                IImageFormat x = await newImage.SaveAsync(responseStream,
                    this.contentType, encoderQuality: 80L);
                await responseStream.FlushAsync();
                newImage.Dispose();
            }
        }
    }

    [PdfResponse()]
    public delegate IHttpResponse PdfResponse(byte[] content, string name, bool inline);
    public class PdfResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "PDF File data";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            PdfResponse responseDelegate = (pdfData, filename, inline) =>
            {
                var response = new BytesHttpResponse(request, this.StatusCode,
                    fileName: filename, contentType: "application/pdf", inline: inline,
                    data: pdfData);
                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess(responseDelegate);
        }
    }

    [SvgResponse]
    public delegate IHttpResponse SvgResponse(string content, Encoding encoding = default,
        string filename = default, bool? inline = default, bool autoScale = false);
    public class SvgResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "<svg></svg>";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            SvgResponse responseDelegate = (content, encoding, filename, inline, autoscale) =>
            {
                if (autoscale)
                {
                    var doc = XDocument.Load(new StringReader(content));
                    var svgEle = doc.Root;
                    svgEle.SetAttributeValue("width", "100%");
                    svgEle.SetAttributeValue("height", "auto");

                    var saveStream = new MemoryStream();
                    doc.Save(saveStream);
                    content = saveStream.ToArray().GetString();
                }
                var response = new StringHttpResponse(request, this.StatusCode,
                    fileName: filename, contentType: "image/svg+xml", inline: inline,
                    content, encoding);

                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess(responseDelegate);
        }
    }

    #endregion

    #endregion

    [JsonStringResponse]
    public delegate IHttpResponse JsonStringResponse(string content);
    public class JsonStringResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "{ }";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            JsonStringResponse responseDelegate = (json) =>
            {
                var response = new JsonStringHttpResponse(request, this.StatusCode, json);
                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess(responseDelegate);
        }
    }

    #region HTML

    [HtmlResponse]
    public delegate IHttpResponse HtmlResponse(string content);
    public class HtmlResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "<html><head></head><body>Hello World</body></html>";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            HtmlResponse responseDelegate = (html) =>
            {
                var response = new HtmlHttpResponse(request, this.StatusCode, html);
                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess(responseDelegate);
        }
    }

    [ViewFileTypedResponse]
    public delegate IHttpResponse ViewFileResponse<T>(string viewPath, T content);

    public class ViewFileTypedResponseAttribute : HttpGenericDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "<html></html>";

        [InstigateMethod]
        public IHttpResponse ContentResponse<TResource>(string viewPath, TResource content)
        {
            var httpApiApp = this.httpApp as IApiApplication;
            var response = new ViewHttpResponse<TResource>(viewPath, content,
                httpApiApp, this.request,
                this.StatusCode);
            return UpdateResponse(parameterInfo, httpApp, request, response);
        }

        protected class ViewHttpResponse<T> : HttpResponse
        {
            public ViewHttpResponse(
                    string viewPath, T model,
                    IApiApplication httpApiApp,
                    IHttpRequest request, HttpStatusCode statusCode)
                : base(request, statusCode,
                      async (responseStream) =>
                      {
                          try
                          {
                              var path = httpApiApp.HostEnvironment.ContentRootPath + Path.DirectorySeparatorChar + "Pages" + Path.DirectorySeparatorChar + viewPath;
                              
                              if (!TryFindView(httpApiApp.HostEnvironment, viewPath, out string fullViewPath))
                              {
                                  using (var output = new StreamWriter(responseStream))
                                  {
                                      await output.WriteAsync($"<html><head><title>Could not find file with path:{viewPath}</title></head>");
                                      await output.WriteAsync($"<body><div>Could not find file with path:{viewPath}</div></body></html>");
                                      await output.FlushAsync();
                                      return;
                                  }
                              }
                              
                              var razorSource = await File.ReadAllTextAsync(fullViewPath);

                              var razorEngine = new RazorEngineCore.RazorEngine();
                              var template = razorEngine.Compile<RazorEngineCore.RazorEngineTemplateBase<T>>(razorSource,
                                  builder =>
                                  {
                                      var assemblies = httpApiApp.GetResources()
                                        .Select(res => res.Assembly)
                                        .Append(model.GetType().Assembly)
                                        .Append(Assembly.GetExecutingAssembly())

                                        // East Five libs
                                        .Append(typeof(EastFive.IReferenceable).Assembly)
                                        .Append(typeof(EastFive.Api.IApiApplication).Assembly)

                                        // Core runtime stuff
                                        .Append(typeof(object).Assembly) // include corlib  "System.Runtime.dll
                                        .Append(typeof(RazorCompiledItemAttribute).Assembly) // include Microsoft.AspNetCore.Razor.Runtime
                                        .Append(Assembly.Load(new AssemblyName("Microsoft.CSharp")))
                                        // as found out by @Isantipov, for some other reason on .NET Core for Mac and Linux, we need to add this... this is not needed with .NET framework
                                        .Append(Assembly.Load(new AssemblyName("netstandard")))
                                        .Append(Assembly.Load(new AssemblyName("System.Runtime")))
                                        .Append(typeof(HttpStatusCode).Assembly)
                                        .Append(typeof(HttpUtility).Assembly)
                                        .Append(typeof(Uri).Assembly)
                                        .Distinct(assembly => assembly.FullName);

                                      foreach (var assembly in assemblies)
                                          builder.AddAssemblyReference(assembly);

                                  });

                              var html = template.Run(
                                  instance =>
                                  {
                                      instance.Model = model;
                                  });

                              //using (var output = new StreamWriter(new StreamAsyncWrapper(responseStream)))
                              using (var output = new StreamWriter(responseStream))
                              {
                                  await output.WriteAsync(html);
                                  await output.FlushAsync();
                              }
                          } catch(Exception ex)
                          {
                              using (var output = new StreamWriter(responseStream))
                              {
                                  await output.WriteAsync($"<html><head><title>{ex.Message}</title></head>");
                                  await output.WriteAsync($"<body><code>{ex.StackTrace}</code></body></html>");
                                  await output.FlushAsync();
                              }
                          }
                      })
            {
                this.SetContentType("text/html");
            }

            private static bool TryFindView(Microsoft.Extensions.Hosting.IHostEnvironment env, string viewPath,
                out string fullViewPath)
            {
                bool TryPath(string path, out string fullPath)
                {
                    var slashPath = $"{env.ContentRootPath}{Path.DirectorySeparatorChar}{path}{Path.DirectorySeparatorChar}{viewPath}";
                    fullPath = slashPath.Replace('/', Path.DirectorySeparatorChar);
                    return File.Exists(fullPath);
                }

                if (TryPath("Pages", out fullViewPath))
                    return true;

                if (TryPath("Meta", out fullViewPath))
                    return true;

                return TrySearchView(string.Empty, out fullViewPath);

                bool TrySearchView(string directoryPath, out string pathFound)
                {
                    var fullDirPath = env.ContentRootPath + Path.DirectorySeparatorChar + directoryPath;
                    foreach (var dirInfo in Directory.EnumerateDirectories(fullDirPath))
                    {
                        var dirName = dirInfo.Substring(fullDirPath.Length).Trim('\\');
                        var tryPath = $"{directoryPath}/{dirName}";
                        if (TryPath(tryPath, out pathFound))
                            return true;
                        if (TrySearchView(directoryPath + Path.DirectorySeparatorChar + dirName, out pathFound))
                            return true;
                    }
                    pathFound = default;
                    return false;
                }
            }
        }
    }

    [WebrootHtmlResponse]
    public delegate IHttpResponse WebrootHtmlResponse(string filename, 
        Func<Stream, Task<string>> manipulateHtml);

    public class WebrootHtmlResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "<html></html>";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            WebrootHtmlResponse responseDelegate =
                (filename, manipulateHtml) =>
                {
                    var httpApiApp = httpApp as IApiApplication;
                    
                    var response = new CallbackResponse(request, this.StatusCode,
                            httpApiApp, filename, manipulateHtml);
                        return UpdateResponse(parameterInfo, httpApp, request, response);
                };
            return onSuccess(responseDelegate);
        }

        class CallbackResponse : HttpResponse
        {
            public CallbackResponse(IHttpRequest request, HttpStatusCode statusCode,
                IApiApplication httpApiApp,
                string filename, Func<Stream, Task<string>> onFound)
                : base(request, statusCode,
                      async outstream =>
                      {
                          var fileInfo = httpApiApp.HostEnvironment.ContentRootFileProvider.GetFileInfo(
                              "wwwroot" + Path.DirectorySeparatorChar + filename);
                          using (var fileStream = fileInfo.CreateReadStream())
                          {
                              var resultHtml = await onFound(fileStream);
                              var responseBytes = resultHtml.GetBytes();
                              await outstream.WriteAsync(responseBytes, 0, responseBytes.Length);
                          }
                      })
            {
            }

        }
    }

    [GalleryResponse]
    public delegate IHttpResponse GalleryResponse(IEnumerable<Image> files,
        string mimeType = default, int? imagesPerLine = default(int?));
    public class GalleryResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Raw data (byte [])";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            GalleryResponse responseDelegate = (files, mimeType, imagesPerLine) =>
            {
                var response = new GalleryHttpResponse(request,
                    images: files, mimeType: mimeType, imagesPerLine: imagesPerLine);

                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess(responseDelegate);
        }
    }

    [LinkedGalleryResponse]
    public delegate IHttpResponse LinkedGalleryResponse(IEnumerable<(Image, Uri)> files,
        string mimeType = default, int? imagesPerLine = default(int?));
    public class LinkedGalleryResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Raw data (byte [])";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            LinkedGalleryResponse responseDelegate = (files, mimeType, imagesPerLine) =>
            {
                var response = new LinkedGalleryHttpResponse(request,
                    images: files, mimeType: mimeType, imagesPerLine: imagesPerLine);

                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess(responseDelegate);
        }
    }

    [LinksResponse]
    public delegate IHttpResponse LinksResponse(IEnumerable<Uri> links);
    public class LinksResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            LinksResponse responseDelegate = (links) =>
            {
                var response = new LinksHttpResponse(request, links: links);

                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess(responseDelegate);
        }
    }

    //public class ViewFileResponseAttribute : HtmlResponseAttribute
    //{
    //    public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
    //            IHttpRequest request, ParameterInfo parameterInfo,
    //        Func<object, Task<IHttpResponse>> onSuccess)
    //    {
    //        ViewFileResponse responseDelegate =
    //            (filePath, content) =>
    //            {
    //                var viewEngine = request.RazorViewEngine;
    //                var httpApiApp = httpApp as IApiApplication;
    //                return new ViewHttpResponse(filePath, content,
    //                    httpApiApp, viewEngine,
    //                    request, HttpStatusCode.OK);

    //                //var viewEngineResult = viewEngine.GetView(path, filePath, false);

    //                //if (!viewEngineResult.Success)
    //                //    return request.CreateResponse(HttpStatusCode.InternalServerError, $"Couldn't find view {filePath}");

    //                //var view = viewEngineResult.View;

    //                //var viewContext = new ViewContext();
    //                //viewContext.HttpContext = (request as Core.CoreHttpRequest).request.HttpContext;
    //                ////viewContext.ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary<TModel>(
    //                ////    new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), 
    //                ////    new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
    //                ////{ Model = content };
    //                //viewContext.ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
    //                //    new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
    //                //    new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
    //                //{ Model = content };
    //                //return new ViewHttpResponse(view, viewContext, request, this.StatusCode);

    //            };
    //        return onSuccess(responseDelegate);
    //    }

    //    protected class ViewHttpResponse<T> : HttpResponse
    //    {
    //        public ViewHttpResponse(
    //                string viewPath, T model,
    //                IApiApplication httpApiApp,
    //                IHttpRequest request, HttpStatusCode statusCode)
    //            : base(request, statusCode,
    //                  async (responseStream) =>
    //                  {
    //                      var path = httpApiApp.HostEnvironment.ContentRootPath + Path.DirectorySeparatorChar + "Pages" + Path.DirectorySeparatorChar + viewPath;
    //                      var fullViewPath = path.Replace('/', Path.DirectorySeparatorChar);
    //                      var razorSource = await File.ReadAllTextAsync(fullViewPath);

    //                      var razorEngine = new RazorEngineCore.RazorEngine();
    //                      var template = razorEngine.Compile<RazorEngineCore.RazorEngineTemplateBase<T>>(razorSource,
    //                          builder =>
    //                          {
    //                              //MetadataReference.CreateFromFile(typeof(object).Assembly), // include corlib
    //                              //builder.AddAssemblyReferenceByName("System.Security"); // by name
    //                              //builder.AddAssemblyReference(typeof(System.IO.File)); // by type
    //                              var assemblies = httpApiApp.GetResources()
    //                                .Select(res => res.Assembly)
    //                                .Append(typeof(EastFive.IRef).Assembly)
    //                                .Append(typeof(EastFive.Api.IApiApplication).Assembly)
    //                                .Append(model.GetType().Assembly)
    //                                .Append(typeof(object).Assembly) // include corlib  "System.Runtime.dll
    //                                .Append(typeof(RazorCompiledItemAttribute).Assembly) // include Microsoft.AspNetCore.Razor.Runtime
    //                                .Append(Assembly.Load(new AssemblyName("Microsoft.CSharp")))
    //                                .Append(Assembly.Load(new AssemblyName("netstandard")))
    //                                .Append(Assembly.Load(new AssemblyName("System.Runtime")))
    //                                .Distinct(ass => ass.FullName);

    //                //        MetadataReference.CreateFromFile(typeof(RazorCompiledItemAttribute).Assembly.Location), // include Microsoft.AspNetCore.Razor.Runtime
    //                //        MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location), // this file (that contains the MyTemplate base class)

    //                //        // for some reason on .NET core, I need to add this... this is not needed with .NET framework
    //                //        MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll")),

    //                //        // as found out by @Isantipov, for some other reason on .NET Core for Mac and Linux, we need to add this... this is not needed with .NET framework
    //                //        MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "netstandard.dll"))

    //                              foreach (var assembly in assemblies)
    //                                builder.AddAssemblyReference(assembly);

    //                          });

    //                      var html = template.Run(
    //                          instance =>
    //                          {
    //                              instance.Model = model;
    //                          });

    //                      using (var output = new StreamWriter(responseStream))
    //                      {
    //                          await output.WriteAsync(html);
    //                          await output.FlushAsync();
    //                      }
    //                  })
    //        {
    //        }
    //    }

    //}

    //[ViewStringResponse]
    //public delegate IHttpResponse ViewStringResponse(string razorSource, object content);
    //public class ViewStringResponseAttribute : ViewFileResponseAttribute
    //{
    //    public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
    //            IHttpRequest request, ParameterInfo parameterInfo,
    //        Func<object, Task<IHttpResponse>> onSuccess)
    //    {
    //        ViewFileResponse responseDelegate =
    //            (razorSource, content) =>
    //            {
    //                throw new NotImplementedException();
    //                //var razorEngine = new RazorEngineCore.RazorEngine();
    //                //var template = razorEngine.Compile(razorSource);

    //                //var html = template.Run(content);
    //                //return new ViewHtmlResponse(html, request, HttpStatusCode.OK);

    //                //// points to the local path
    //                //var fs = RazorProjectFileSystem.Create(".");

    //                //// customize the default engine a little bit
    //                //var engine = RazorProjectEngine.Create(RazorConfiguration.Default, fs, (builder) =>
    //                //{
    //                //    // InheritsDirective.Register(builder); // in .NET core 3.1, compatibility has been broken (again), and this is not needed anymore...
    //                //    builder.SetNamespace("EastFive"); // define a namespace for the Template class
    //                //});

    //                //// get a razor-templated file. My "hello.txt" template file is defined like this:
    //                ////
    //                //// @inherits RazorTemplate.MyTemplate
    //                //// Hello @Model.Name, welcome to Razor World!
    //                ////

    //                //var item = fs.GetItem("hello.txt");

    //                //// parse and generate C# code
    //                //var codeDocument = engine.Process(item);
    //                //var cs = codeDocument.GetCSharpDocument();

    //                //// outputs it on the console
    //                ////Console.WriteLine(cs.GeneratedCode);

    //                //// now, use roslyn, parse the C# code
    //                //var tree = CSharpSyntaxTree.ParseText(cs.GeneratedCode);

    //                //// define the dll
    //                //const string dllName = "hello";
    //                //var compilation = CSharpCompilation.Create(dllName, new[] { tree },
    //                //    new[]
    //                //    {
    //                //        MetadataReference.CreateFromFile(typeof(object).Assembly.Location), // include corlib
    //                //        MetadataReference.CreateFromFile(typeof(RazorCompiledItemAttribute).Assembly.Location), // include Microsoft.AspNetCore.Razor.Runtime
    //                //        MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location), // this file (that contains the MyTemplate base class)

    //                //        // for some reason on .NET core, I need to add this... this is not needed with .NET framework
    //                //        MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll")),

    //                //        // as found out by @Isantipov, for some other reason on .NET Core for Mac and Linux, we need to add this... this is not needed with .NET framework
    //                //        MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "netstandard.dll"))
    //                //    },
    //                //    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)); // we want a dll

    //                //// compile the dll
    //                //string path = Path.Combine(Path.GetFullPath("."), dllName + ".dll");
    //                //var result = compilation.Emit(path);
    //                //if (!result.Success)
    //                //{
    //                //    var errorText = string.Join(Environment.NewLine, result.Diagnostics);
    //                //    return new ViewHtmlResponse(errorText, request, HttpStatusCode.InternalServerError);
    //                //}

    //                //// load the built dll
    //                //Console.WriteLine(path);
    //                //var asm = Assembly.LoadFile(path);

    //                //// the generated type is defined in our custom namespace, as we asked. "Template" is the type name that razor uses by default.
    //                //var template = Activator.CreateInstance(asm.GetType("EastFive.Template"));

    //                //// run the code.
    //                //// should display "Hello Killroy, welcome to Razor World!"
    //                //template.ExecuteAsync().Wait();
    //                //return new ViewHtmlResponse(errorText, request, HttpStatusCode.InternalServerError);

    //            };
    //        return onSuccess(responseDelegate);
    //    }

    //    class ViewHtmlResponse : HttpResponse
    //    {
    //        public ViewHtmlResponse(
    //                string viewPath, object model,
    //                IApiApplication httpApiApp, IRazorViewEngine viewEngine,
    //                IHttpRequest request, HttpStatusCode statusCode)
    //            : base(request, statusCode,
    //                  async (responseStream) =>
    //                  {
    //                      var path = httpApiApp.HostEnvironment.ContentRootPath + Path.DirectorySeparatorChar + "Pages" + Path.DirectorySeparatorChar + viewPath;
    //                      var fullViewPath = viewPath.Replace('/', Path.DirectorySeparatorChar);
    //                      var razorSource = await File.ReadAllTextAsync(fullViewPath);

    //                      var razorEngine = new RazorEngineCore.RazorEngine();
    //                      var template = razorEngine.Compile(razorSource);

    //                      var html = template.Run(model);

    //                      using (var output = new StreamWriter(responseStream))
    //                      {
    //                          await output.WriteAsync(html);
    //                          await output.FlushAsync();
    //                      }
    //                  })
    //        {
    //        }
    //    }
    //}

    #endregion

    //[XlsxResponse()]
    //public delegate IHttpResponse XlsxResponse(byte[] content, string name);
    //public class XlsxResponseAttribute : HttpFuncDelegateAttribute
    //{
    //    public override HttpStatusCode StatusCode => HttpStatusCode.OK;

    //    public override string Example => "<xml></xml>";

    //    public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
    //            IHttpRequest request, ParameterInfo parameterInfo,
    //        Func<object, Task<IHttpResponse>> onSuccess)
    //    {
    //        XlsxResponse responseDelegate = (xlsxData, filename) =>
    //        {
    //            var response = new BytesHttpResponse(request, this.StatusCode,
    //                fileName: filename.IsNullOrWhiteSpace() ? $"sheet.xlsx" : filename, 
    //                contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.template",
    //                inline: false,
    //                xlsxData);
    //            return UpdateResponse(parameterInfo, httpApp, request, response);
    //        };
    //        return onSuccess((object)responseDelegate);
    //    }
    //}

    #region Multipart

    [MultipartAsyncResponseGenericCSV]
    public delegate IHttpResponse MultipartAsyncResponseCsv<TResource>(IEnumerableAsync<TResource> responses,
        string fileName = default, bool includeHeaders = true, bool inline = false);
    public class MultipartAsyncResponseGenericCSVAttribute : HttpGenericDelegateAttribute, IProvideResponseType
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "[]";

        [InstigateMethod]
        public IHttpResponse EnumerableAsyncHttpResponse<T>(IEnumerableAsync<T> objectsAsync,
            string fileName, bool includeHeaders, bool inline)
        {
            var response = new EnumerableAsyncCsvResponse<T>(this.httpApp, request, this.parameterInfo,
                this.StatusCode,
                objectsAsync,
                fileName:fileName, includeHeaders: includeHeaders, inline: inline);
            return UpdateResponse(parameterInfo, httpApp, request, response);
        }

        public override Response GetResponse(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var baseResponse = base.GetResponse(paramInfo, httpApp);
            baseResponse.IsMultipart = true;
            return baseResponse;
        }

        public Type GetResponseType(ParameterInfo parameterInfo)
        {
            return parameterInfo.ParameterType.GenericTypeArguments.First();
        }
    }

    [MultipartAsyncResponseGeneric]
    public delegate IHttpResponse MultipartAsyncResponse<TResource>(IEnumerableAsync<TResource> responses);
    public class MultipartAsyncResponseGenericAttribute : HttpGenericDelegateAttribute, IProvideResponseType
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "[]";

        [InstigateMethod]
        public IHttpResponse EnumerableAsyncHttpResponse<T>(IEnumerableAsync<T> objectsAsync)
        {
            var response = new EnumerableAsyncHttpResponse<T>(this.httpApp, request, this.parameterInfo,
                this.StatusCode,
                objectsAsync);
            return UpdateResponse(parameterInfo, httpApp, request, response);
        }

        public override Response GetResponse(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var baseResponse = base.GetResponse(paramInfo, httpApp);
            baseResponse.IsMultipart = true;
            return baseResponse;
        }

        public Type GetResponseType(ParameterInfo parameterInfo)
        {
            return parameterInfo.ParameterType.GenericTypeArguments.First();
        }
    }

    [MultipartAcceptArrayResponse]
    public delegate IHttpResponse MultipartAcceptArrayResponse(IEnumerable<object> responses);
    public class MultipartAcceptArrayResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "[]";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            MultipartAcceptArrayResponse responseDelegate =
                (objects) =>
                {
                    var objectsArr = objects.ToArray();
                    var response = new JsonHttpResponse(request, this.StatusCode, objectsArr);
                    return UpdateResponse(parameterInfo, httpApp, request, response);
                };
            return onSuccess(responseDelegate);
        }
    }

    [MultipartAcceptArrayResponseType]
    public delegate IHttpResponse MultipartAcceptArrayResponse<TResource>(IEnumerable<TResource> responses);
    public class MultipartAcceptArrayResponseTypeAttribute : HttpGenericDelegateAttribute, IProvideResponseType
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "[]";

        [InstigateMethod]
        public IHttpResponse MultipartAcceptArrayResponse<TResource>(IEnumerable<TResource> responses)
        {
            var objectsArr = responses.ToArray();
            var response = new JsonHttpResponse(request, this.StatusCode, objectsArr);
            return UpdateResponse(parameterInfo, httpApp, request, response);
        }

        public override Response GetResponse(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var baseResponse = base.GetResponse(paramInfo, httpApp);
            baseResponse.IsMultipart = true;
            return baseResponse;
        }

        public Type GetResponseType(ParameterInfo parameterInfo)
        {
            return parameterInfo.ParameterType.GenericTypeArguments.First();
        }
    }

    [ServerSideEventsResponseGeneric]
    public delegate IHttpResponse ServerSideEventsResponse<TResource>(IEnumerableAsync<TResource> responses);
    public class ServerSideEventsResponseGenericAttribute : HttpGenericDelegateAttribute, IProvideResponseType
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "[]";

        [InstigateMethod]
        public IHttpResponse EnumerableAsyncHttpResponse<T>(IEnumerableAsync<T> objectsAsync)
        {
            var response = new ServerSideEventsEnumerableAsyncHttpResponse<T>(this.httpApp, request, this.parameterInfo,
                this.StatusCode,
                objectsAsync);
            return UpdateResponse(parameterInfo, httpApp, request, response);
        }

        public override Response GetResponse(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var baseResponse = base.GetResponse(paramInfo, httpApp);
            baseResponse.IsMultipart = true;
            baseResponse.Headers = baseResponse.Headers
                .Append("Content-Type".PairWithValue("text/event-stream"))
                .ToArray();
            return baseResponse;
        }

        public Type GetResponseType(ParameterInfo parameterInfo)
        {
            return parameterInfo.ParameterType.GenericTypeArguments.First();
        }
    }

    [ServerSideEventsWithCallbackResponseGeneric]
    public delegate IHttpResponse ServerSideEventsWithCallbackResponse<TResource>(IEnumerableAsync<TResource> responses,
        Func<TResource, CancellationToken, Task> onFinished);
    public class ServerSideEventsWithCallbackResponseGenericAttribute : HttpGenericDelegateAttribute, IProvideResponseType
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "[]";

        [InstigateMethod]
        public IHttpResponse EnumerableAsyncHttpResponse<T>(
            IEnumerableAsync<T> objectsAsync, Func<T, CancellationToken, Task> onFinished)
        {
            var response = new ServerSideEventsEnumerableAsyncCallbackHttpResponse<T>(this.httpApp, request, this.parameterInfo,
                this.StatusCode,
                objectsAsync, onFinished);
            return UpdateResponse(parameterInfo, httpApp, request, response);
        }

        public override Response GetResponse(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var baseResponse = base.GetResponse(paramInfo, httpApp);
            baseResponse.IsMultipart = true;
            baseResponse.Headers = baseResponse.Headers
                .Append("Content-Type".PairWithValue("text/event-stream"))
                .ToArray();
            return baseResponse;
        }

        public Type GetResponseType(ParameterInfo parameterInfo)
        {
            return parameterInfo.ParameterType.GenericTypeArguments.First();
        }
    }

    #endregion

    #region Zip

    [ZipResponse]
    public delegate IHttpResponse ZipResponse(IEnumerable<(FileInfo, byte[])> files,
        string filename = default, bool? inline = default);
    public class ZipResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Raw data (byte [])";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            ZipResponse responseDelegate = (files, filename, inline) =>
            {
                var response = new ZipHttpResponse(request,
                    fileName: filename, inline: inline,
                    files);

                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess(responseDelegate);
        }
    }

    [ZipAsyncResponse]
    public delegate IHttpResponse ZipAsyncResponse(IEnumerableAsync<(string, Func<Stream, Task>)> files,
        string filename = default, bool? inline = default);
    public class ZipAsyncResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override string Example => "Raw data (byte [])";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            ZipAsyncResponse responseDelegate = (files, filename, inline) =>
            {
                var response = new ZipAsyncHttpResponse(request,
                    fileName: filename, inline: inline,
                    files);

                return UpdateResponse(parameterInfo, httpApp, request, response);
            };
            return onSuccess(responseDelegate);
        }
    }

    #endregion
}

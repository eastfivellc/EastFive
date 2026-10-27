using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading.Tasks;
using System.Net.Http.Headers;

using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Api;
using EastFive.Api.Azure.Resources;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Images;
using EastFive.Web.Configuration;
using EastFive.Azure.Persistence.Blobs;
using EastFive.Persistence.Azure.StorageTables.Driver;

namespace EastFive.Azure.Media
{
    public static class ContentExtensions
    {
        //public static Task<TResult> LoadBytesAsync<TResult>(this IRef<Content> contentRef,
        //    Func<byte [], string, TResult> onFound,
        //    Func<TResult> onNotFound)
        //{
        //    return contentRef.id.BlobLoadBytesAsync("content",
        //        onFound,
        //        onNotFound);
        //}

        //public static Task<TResult> LoadStreamAsync<TResult>(this IRef<Content> contentRef,
        //    Func<Stream, string, TResult> onFound,
        //    Func<TResult> onNotFound)
        //{
        //    return contentRef.id.BlobLoadStreamAsync("content",
        //        onFound,
        //        onNotFound);
        //}

        public static Task<TResult> LoadImageAsync<TResult>(this IBlobRef contentRef,
            Func<System.Drawing.Image, MediaTypeHeaderValue, TResult> onFound,
            Func<TResult> onNotFound,
            Func<Stream, MediaTypeHeaderValue, TResult> onInvalidImage = default)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

            #pragma warning disable CA1416
            return contentRef.LoadStreamAsync(
                (blobName, imageStream, contentType, contentDisposition) =>
                {
                    if(imageStream.TryReadImage(out Image image))
                        return onFound(image, contentType);

                    if(onInvalidImage.IsDefaultOrNull())
                        return onNotFound();

                    return onInvalidImage(imageStream, contentType);
                },
                onNotFound);
            #pragma warning restore CA1416
        }

        public static Task<TResult> LoadImageAsync<TResult>(this IBlobRef blobRef,
            Func<System.Drawing.Image, MediaTypeHeaderValue, ContentDispositionHeaderValue, TResult> onFound,
            Func<TResult> onNotFound,
            Func<byte [], MediaTypeHeaderValue, ContentDispositionHeaderValue, TResult> onInvalidImage = default)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

            #pragma warning disable CA1416
            return blobRef.LoadBytesAsync(
                (blobId, imageBytes, mediaType, disposition) =>
                {
                    if (imageBytes.TryReadImage(out Image image))
                        return onFound(image, mediaType, disposition);

                    if (onInvalidImage.IsDefaultOrNull())
                        return onNotFound();

                    return onInvalidImage(imageBytes, mediaType, disposition);
                },
                onNotFound);
            #pragma warning restore CA1416
        }

        /// <summary>
        /// The blob decoded with Skia. The bitmap is NOT oriented: for an EXIF-rotated photo,
        /// read the origin from the bytes and apply <c>FixOrientation</c>.
        /// </summary>
        public static Task<TResult> LoadSkiaImageAsync<TResult>(this IBlobRef blobRef,
            Func<SkiaSharp.SKBitmap, MediaTypeHeaderValue, ContentDispositionHeaderValue, SkiaSharp.SKEncodedImageFormat, TResult> onFound,
            Func<TResult> onNotFound,
            Func<byte[], MediaTypeHeaderValue, ContentDispositionHeaderValue, TResult> onInvalidImage = default)
        {
            return blobRef.LoadBytesAsync(
                (blobId, imageBytes, mediaType, disposition) =>
                {
                    if (imageBytes.TryReadImage(out SkiaSharp.SKBitmap image, out SkiaSharp.SKEncodedImageFormat format))
                        return onFound(image, mediaType, disposition, format);

                    if (onInvalidImage.IsDefaultOrNull())
                        return onNotFound();

                    return onInvalidImage(imageBytes, mediaType, disposition);
                },
                onNotFound);
        }

        public static Task<TResult> LoadSkiaImageAsync<TResult>(this IBlobRef blobRef,
                Func<SkiaSharp.SKBitmap, MediaTypeHeaderValue, ContentDispositionHeaderValue, TResult> onFound,
                Func<TResult> onNotFound,
                Func<byte[], MediaTypeHeaderValue, ContentDispositionHeaderValue, TResult> onInvalidImage = default) =>
            blobRef.LoadSkiaImageAsync(
                (image, mediaType, disposition, format) => onFound(image, mediaType, disposition),
                onNotFound:onNotFound,
                onInvalidImage: onInvalidImage);

        public static Task<TResult> LoadImageMagickAsync<TResult>(this IBlobRef blobRef,
            Func<object, MediaTypeHeaderValue, ContentDispositionHeaderValue, object, TResult> onFound,
            Func<TResult> onNotFound,
            Func<byte[], MediaTypeHeaderValue, ContentDispositionHeaderValue, TResult> onInvalidImage = default)
        {
            throw new NotImplementedException();
            //return blobRef.LoadBytesAsync(
            //    (blobId, imageBytes, mediaType, disposition) =>
            //    {
            //        if (imageBytes.TryReadImage(out MagickImage image, out MagickFormat format))
            //            return onFound(image, mediaType, disposition, format);

            //        if (onInvalidImage.IsDefaultOrNull())
            //            return onNotFound();

            //        return onInvalidImage(imageBytes, mediaType, disposition);
            //    },
            //    onNotFound);
        }

        //public static Task<IRef<Content>> ContentCreateAsync(this byte[] content,
        //    string contentType = default,
        //    AzureTableDriverDynamic.RetryDelegate onTimeout = null)
        //{
        //    return content.BlobCreateAsync("content",
        //        (contentId) => contentId.AsRef<Content>(),
        //        contentType: contentType,
        //        onTimeout: onTimeout);
        //}

        //public static Task<TResult> SaveBytesAsync<TResult>(this IRef<Content> contentRef, 
        //        byte[] content,
        //    Func<TResult> onSuccess,
        //    Func<TResult> onAlreadyExists = default,
        //    Func<Persistence.StorageTables.ExtendedErrorInformationCodes, string, TResult> onFailure = default,
        //    string contentType = default,
        //    AzureTableDriverDynamic.RetryDelegate onTimeout = null)
        //{
        //    return content.BlobCreateAsync(contentRef.id, "content",
        //        onSuccess,
        //        onAlreadyExists: onAlreadyExists,
        //        onFailure: onFailure,
        //        contentType: contentType,
        //        onTimeout: onTimeout);
        //}

        //public static Task<TResult> SaveStreamAsync<TResult>(this IRef<Content> contentRef,
        //        Stream content,
        //    Func<TResult> onSuccess,
        //    Func<TResult> onAlreadyExists = default,
        //    Func<Persistence.StorageTables.ExtendedErrorInformationCodes, string, TResult> onFailure = default,
        //    string contentType = default,
        //    AzureTableDriverDynamic.RetryDelegate onTimeout = null)
        //{
        //    return content.BlobCreateAsync(contentRef.id, "content",
        //        onSuccess,
        //        onAlreadyExists: onAlreadyExists,
        //        onFailure: onFailure,
        //        contentType: contentType,
        //        onTimeout: onTimeout);
        //}

        public static Task<TResult> AnalyzeAsync<TResult>(this IBlobRef contentRef,
            Func<ImageAnalysis, double?, TResult> onAnalyzed,
            Func<TResult> onInvalidFormat = default,
            Func<TResult> onNotFound = default)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                throw new NotSupportedException("OS not supported");

            return WithComputerVision(
                        async (subscriptionKey, endpointUri) =>
                        {
                            using (var computerVision = new ComputerVisionClient(
                                new ApiKeyServiceClientCredentials(subscriptionKey),
                                new System.Net.Http.DelegatingHandler[] { }))
                            {
                                return await await contentRef.LoadBytesAsync(
                                    async (blobId, imageData, contentType, disposition) =>
                                    {
                                        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                                            throw new NotSupportedException("OS not supported");

                                        var widthMultiplier = default(double?);
                                        if (imageData.Length > 4000000)
                                        {
                                            if (!imageData.TryReadImage(out Image image))
                                            {
                                                if (onInvalidFormat.IsNotDefaultOrNull())
                                                    return onInvalidFormat();
                                                if (onNotFound.IsNotDefaultOrNull())
                                                    return onNotFound();

                                                throw new ArgumentException($"Blob `{blobId}` is not an image.");
                                            }
                                            using (var newImageStream = new MemoryStream())
                                            {
                                                #pragma warning disable CA1416
                                                widthMultiplier = Math.Sqrt(4000000.0 / imageData.Length);
                                                image
                                                    .ResizeImage(
                                                        (int)(image.Width * widthMultiplier),
                                                        (int)(image.Height * widthMultiplier))
                                                    .Save(newImageStream, System.Drawing.Imaging.ImageFormat.Jpeg);
                                                newImageStream.Position = 0;
                                                imageData = await newImageStream.ToBytesAsync();
                                                #pragma warning restore CA1416
                                            }
                                        }
                                        computerVision.Endpoint = endpointUri.OriginalString;
                                        var featuresToSearchFor = ((VisualFeatureTypes?)VisualFeatureTypes.Categories)
                                                .AsArray()
                                                .Append(VisualFeatureTypes.Description)
                                                .Append(VisualFeatureTypes.ImageType)
                                                .Append(VisualFeatureTypes.Objects)
                                                .Append(VisualFeatureTypes.Tags)
                                                .Append(VisualFeatureTypes.Brands)
                                                .Append(VisualFeatureTypes.Color)
                                                .Append(VisualFeatureTypes.Faces)
                                                .ToList();
                                        try
                                        {
                                            using (var imageStream = new MemoryStream(imageData))
                                            {
                                                var analysis = await computerVision.AnalyzeImageInStreamAsync(
                                                    imageStream, featuresToSearchFor);
                                                return onAnalyzed(analysis, widthMultiplier);
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            throw;
                                        }
                                    },
                                    () =>
                                    {
                                        if (onNotFound.IsDefaultOrNull())
                                            throw new ResourceNotFoundException();
                                        return onNotFound().AsTask();
                                    });
                            }
                        });
        }

        public static Task<TResult> AnalyzeAsync<TResult>(this IBlobRef contentRef,
            Func<ImageAnalysis, double?, TResult> onAnalyzed,
            Func<TResult> onNotFound = default)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

            return WithComputerVision(
                        async (subscriptionKey, endpointUri) =>
                        {
                            using (var computerVision = new ComputerVisionClient(
                                new ApiKeyServiceClientCredentials(subscriptionKey),
                                new System.Net.Http.DelegatingHandler[] { }))
                            {
                                return await await contentRef.LoadStreamAsync(
                                    async (blobName, imageStream, contentType, contentDisposition) =>
                                    {
                                        var widthMultiplier = default(double?);
                                        if (imageStream.Length > 4000000)
                                        {
                                            #pragma warning disable CA1416
                                            var image = System.Drawing.Image.FromStream(imageStream);
                                            var newImageStream = new MemoryStream();
                                            widthMultiplier = Math.Sqrt(4000000.0 / imageStream.Length);
                                            image
                                                .ResizeImage(
                                                    (int)(image.Width * widthMultiplier),
                                                    (int)(image.Height * widthMultiplier))
                                                .Save(newImageStream, System.Drawing.Imaging.ImageFormat.Jpeg);
                                            newImageStream.Position = 0;
                                            imageStream = newImageStream;
                                            #pragma warning restore CA1416
                                        }
                                        computerVision.Endpoint = endpointUri.OriginalString;
                                        var featuresToSearchFor = ((VisualFeatureTypes?)VisualFeatureTypes.Categories)
                                                .AsArray()
                                                .Append(VisualFeatureTypes.Description)
                                                .Append(VisualFeatureTypes.ImageType)
                                                .Append(VisualFeatureTypes.Objects)
                                                .Append(VisualFeatureTypes.Tags)
                                                .Append(VisualFeatureTypes.Brands)
                                                .Append(VisualFeatureTypes.Color)
                                                .Append(VisualFeatureTypes.Faces)
                                                .ToList();
                                        try
                                        {
                                            var analysis = await computerVision.AnalyzeImageInStreamAsync(
                                                imageStream, featuresToSearchFor);
                                            return onAnalyzed(analysis, widthMultiplier);
                                        }
                                        catch(Exception)
                                        {
                                            throw;
                                        }
                                    },
                                    () =>
                                    {
                                        if(onNotFound.IsDefaultOrNull())
                                            throw new ResourceNotFoundException();
                                        return onNotFound().AsTask();
                                    });
                            }
                        });
        }
    
        /// <summary>The Computer Vision key and endpoint, from <see cref="ComputerVisionConfiguration"/>.</summary>
        private static Task<TResult> WithComputerVision<TResult>(Func<string, Uri, Task<TResult>> onConfigured)
            => EastFive.Configuration.ConfigurationLoader.LoadAsync<ComputerVisionConfiguration, Task<TResult>>(
                (EastFive.Azure.ComputerVisionConfiguration configuration) => onConfigured(configuration.SubscriptionKey.Reveal(), configuration.Endpoint),
                issue => Unavailable<TResult>(issue), issue => Unavailable<TResult>(issue), issue => Unavailable<TResult>(issue)).Unwrap();

        private static Task<TResult> Unavailable<TResult>(EastFive.Configuration.ConfigIssue issue)
            => throw new EastFive.Web.ConfigurationException(issue.Key, typeof(ComputerVisionConfiguration), issue.ToString());
    }
}

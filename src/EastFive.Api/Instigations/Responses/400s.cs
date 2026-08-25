using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;

using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Reflection;
using EastFive.Linq.Expressions;

namespace EastFive.Api
{
    [BadRequestGenericResponse]
    public delegate IHttpResponse BadRequestResponse<TQuery>(
        Expression<Func<TQuery, bool>> problem);
    public class BadRequestGenericResponseAttribute : HttpGenericDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.BadRequest;

        [InstigateMethod]
        public IHttpResponse BadRequestResponse<TQuery>(Expression<Func<TQuery, bool>> problem)
        {
            Type GetType(Type type)
            {
                if (type.IsArray)
                    return GetType(type.GetElementType());
                return type;
            }

            var response = request.CreateResponse(this.StatusCode);
            return UpdateResponse(parameterInfo, httpApp, request, response);
        }
    }

    [StatusCodeResponse(StatusCode = HttpStatusCode.BadRequest)]
    public delegate IHttpResponse BadRequestResponse();


    [BodyTypeResponse(StatusCode = HttpStatusCode.BadRequest)]
    public delegate IHttpResponse BadRequestBodyResponse<TResource>(TResource content, string contentType = default(string));

    [StatusCodeResponse(StatusCode = HttpStatusCode.Unauthorized)]
    public delegate IHttpResponse UnauthorizedResponse();

    [StatusCodeResponse(StatusCode = HttpStatusCode.Forbidden)]
    public delegate IHttpResponse ForbiddenResponse();

    [StatusCodeResponse(StatusCode = HttpStatusCode.NotFound)]
    public delegate IHttpResponse NotFoundResponse();

    /// <summary>
    /// When performing a query, the document being queried by does not exist.
    /// </summary>
    /// <returns></returns>
    [StatusCodeResponse(StatusCode = HttpStatusCode.NotFound)]
    public delegate IHttpResponse ReferencedDocumentNotFoundResponse();

    /// <summary>
    /// When performing a query, the document being queried by does not exist.
    /// </summary>
    /// <returns></returns>
    [ReferencedDocumentNotFoundResponse]
    public delegate IHttpResponse ReferencedDocumentNotFoundResponse<TResource>();
    public class ReferencedDocumentNotFoundResponseAttribute : HttpGenericDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.NotFound;

        [InstigateMethod]
        public IHttpResponse ReferencedDocumentNotFound<TResource>()
        {
            var response = request.CreateResponse(this.StatusCode);
            return UpdateResponse(parameterInfo, httpApp, request, response);
        }
    }

    /// <summary>
    /// When performing a query, the document being queried by does not exist.
    /// </summary>
    /// <returns></returns>
    [ReferencedDocumentNotFoundGenericResponse]
    public delegate IHttpResponse ReferencedDocumentNotFoundResponse<TQuery, TParameter>(
        Expression<Func<TQuery, TParameter>> problem);
    public class ReferencedDocumentNotFoundGenericResponse : HttpGenericDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.NotFound;

        [InstigateMethod]
        public IHttpResponse ReferencedDocumentNotFoundResponse<TQuery, TParameter>(
            Expression<Func<TQuery, TParameter>> problem)
        {
            return problem.MemberInfo(
                (member) =>
                {
                    var response = request
                        .CreateResponse(this.StatusCode)
                        .AddReason($"There are no resources of type `{member.GetMemberType().FullName}` were found.");
                    return UpdateResponse(parameterInfo, httpApp, request, response);
                },
                () =>
                {
                    var response = request
                        .CreateResponse(this.StatusCode)
                        .AddReason($"Inform server developer that {problem} is not a member expression.");
                    return UpdateResponse(parameterInfo, httpApp, request, response);
                });
        }
    }

    /// <summary>
    /// When creating or updating a resource, a referenced to a different resource was not found.
    /// </summary>
    /// <returns></returns>
    [ReferencedDocumentDoesNotExistResponse]
    public delegate IHttpResponse ReferencedDocumentDoesNotExistsResponse<TResource>();
    public class ReferencedDocumentDoesNotExistResponseAttribute : HttpGenericDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.Conflict;

        [InstigateMethod]
        public IHttpResponse ReferencedDocumentDoesNotExist<TResource>()
        {
            var response = request.CreateResponse(this.StatusCode);
            return UpdateResponse(parameterInfo, httpApp, request, response);
        }
    }

    [StatusCodeResponse(StatusCode = HttpStatusCode.Conflict)]
    public delegate IHttpResponse AlreadyExistsResponse();

    [AlreadyExistsReferencedResponse]
    public delegate IHttpResponse AlreadyExistsReferencedResponse(Guid value);
    public class AlreadyExistsReferencedResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.Conflict;

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            AlreadyExistsReferencedResponse dele =
                (existingId) =>
                {
                    var response = request
                        .CreateResponse(StatusCode)
                        .AddReason($"There is already a resource with ID = [{existingId}]");
                    return UpdateResponse(parameterInfo, httpApp, request, response);
                };
            return onSuccess((object)dele);
        }
    }

    [GeneralConflictResponse]
    public delegate IHttpResponse GeneralConflictResponse(string value);
    public class GeneralConflictResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.Conflict;

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            GeneralConflictResponse responseDelegate =
                (why) =>
                {
                    var response = request.CreateResponse(StatusCode);
                    if (why.IsDefaultNullOrEmpty())
                        return UpdateResponse(parameterInfo, httpApp, request, response);

                    return UpdateResponse(parameterInfo, httpApp, request, response.AddReason(why));
                };
            return onSuccess(responseDelegate);
        }
    }

    [StatusCodeResponse(StatusCode = HttpStatusCode.TooManyRequests)]
    public delegate IHttpResponse TooManyRequestsResponse();
}

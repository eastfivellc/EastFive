using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Net;
using EastFive.Web.Configuration;

namespace EastFive.Api.Meta.Postman.Resources.Collection
{
    public class Collection : IReferenceable
    {
        public Guid id => info._postman_id;

        public Info info { get; set; }
        public Item[] item { get; set; }
        public Variable[] variable { get; set; }

        public static Task<TResult> GetAsync<TResult>(string collectionId,
            Func<Collection, TResult> onFound,
            Func<TResult> onNotFound,
            Func<string, TResult> onFailure = default)
        {
            return EastFive.Api.AppSettings.Postman.ApiKey.ConfigurationString(
                apiKey =>
                {
                    Uri.TryCreate($"https://api.getpostman.com/collections/{collectionId}", UriKind.Absolute, out Uri getCollectionsUri);
                    return getCollectionsUri.HttpClientGetResourceAsync(
                        (CollectionCollection collection) =>
                        {
                            return onFound(collection.collection);
                        },
                        mutateRequest: (request) =>
                        {
                            request.Headers.Add("X-API-Key", apiKey);
                            return request;
                        },
                        onFailureWithBody:(statusCode, body) =>
                        {
                            if (statusCode == System.Net.HttpStatusCode.NotFound)
                                return onNotFound();
                            throw new Exception(body);
                        });
                },
                onUnspecified:onFailure.AsAsyncFunc());
        }

        public Task<TResult> CreateAsync<TResult>(
            Func<CollectionSummary, TResult> onCreated)
        {
            var collection = new CollectionCollection() { collection = this };
            return EastFive.Api.AppSettings.Postman.ApiKey.ConfigurationString(
                apiKey =>
                {
                    Uri.TryCreate($"https://api.getpostman.com/collections", UriKind.Absolute, out Uri getCollectionsUri);
                    return getCollectionsUri.HttpClientPostResourceAsync(collection,
                        (CollectionSummaryParent collectionUpdated, System.Net.Http.HttpResponseMessage httpResponse) =>
                        {
                            return onCreated(collectionUpdated.collection);
                        },
                        mutateRequest: (request) =>
                        {
                            request.Headers.Add("X-API-Key", apiKey);
                            return request;
                        });
                });
        }

        public class CollectionCollection
        {
            public Collection collection;
        }

        public Collection AppendItem(Item itemToAppend,
            string folderName = default)
        {
            var collection = this;
            if(folderName.HasBlackSpace())
                return collection.item
                    .NullToEmpty()
                    .Where(item => folderName.Equals(item.name))
                    .First(
                        (folderItem, next) =>
                        {
                            folderItem.item = folderItem.item
                                .Append(itemToAppend)
                                .ToArray();

                            var collectionItems = collection.item
                                .Where(item => !folderName.Equals(item.name, StringComparison.CurrentCultureIgnoreCase))
                                .Append(folderItem)
                                .ToArray();
                            return new Collection
                            {
                                info = collection.info,
                                item = collectionItems,
                                variable = collection.variable,
                            };
                        },
                        () =>
                        {
                            var folderItem = new Item
                            {
                                name = folderName,
                                item = itemToAppend.AsArray(),
                            };
                            return new Collection
                            {
                                info = collection.info,
                                item = itemToAppend.item
                                    .NullToEmpty()
                                    .Append(folderItem)
                                    .ToArray(),
                                variable = collection.variable,
                            };
                        });

            return new Collection
            {
                info = collection.info,
                item = collection.item.Append(itemToAppend).ToArray(),
                variable = collection.variable,
            };
        }

        public Collection AppendItems(Item[] itemsToAppend,
            string folderName)
        {
            var collection = this;
            var folderItem = collection.item
                .NullToEmpty()
                .Where(item => folderName.Equals(item.name))
                .First(
                    (folderItem, next) => folderItem,
                    () =>
                    {
                        return new Item
                        {
                            name = folderName,
                            item = new Item[] { },
                        };
                    });

            folderItem.item = folderItem.item
                .Concat(itemsToAppend)
                .ToArray();

            var collectionItems = collection.item
                .NullToEmpty()
                .Where(item => !folderName.Equals(item.name))
                .Append(folderItem)
                .ToArray();

            return new Collection
            {
                info = collection.info,
                item = collectionItems,
                variable = collection.variable,
            };
        }

        public static async Task<TResult> CreateOrUpdateMonitoringCollectionAsync<TResult>(
                string name, Uri hostVariable,
                Func<Collection, Collection> modifyCollection,
            Func<CollectionSummary, TResult> onCreatedOrUpdated,
            Func<string, TResult> onFailure)
        {
            return await EastFive.Api.AppSettings.Postman.MonitoringCollectionId.ConfigurationString(
                async collectionId =>
                {
                    return await await EastFive.Api.Meta.Postman.Resources.Collection.Collection.GetAsync(collectionId,
                        collection =>
                        {
                            return modifyCollection(collection)
                                .UpdateAsync<TResult>(
                                    (updatedCollection) =>
                                    {
                                        return onCreatedOrUpdated(updatedCollection);
                                    },
                                    onFailure: onFailure);
                        },
                        () =>
                        {
                            var collection = modifyCollection(new Collection()
                            {
                                info = new Info
                                {
                                    name = name,
                                    schema = "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
                                    // _postman_id = collectionRef.id,
                                },
                                variable = new Variable[]
                                {
                                    new Variable
                                    {
                                        id = Url.VariableHostName,
                                        key = Url.VariableHostName,
                                        value = hostVariable.BaseUri().OriginalString,
                                        type = "string",
                                    }
                                },
                                item = new Item[] { }
                            });
                            return collection.CreateAsync(
                                (createdCollection) =>
                                {
                                    return onCreatedOrUpdated(createdCollection);
                                });
                        },
                        onFailure: onFailure.AsAsyncFunc());
                },
                onUnspecified: onFailure.AsAsyncFunc());
        }

        public Task<TResult> UpdateAsync<TResult>(
            Func<CollectionSummary, TResult> onFound,
            Func<string, TResult> onFailure)
        {
            var collection = new CollectionCollection() { collection = this };
            return EastFive.Api.AppSettings.Postman.ApiKey.ConfigurationString(
                apiKey =>
                {
                    Uri.TryCreate($"https://api.getpostman.com/collections/{collection.collection.id}", UriKind.Absolute, out Uri getCollectionsUri);
                    return getCollectionsUri.HttpClientPutResourceAsync(collection,
                        (CollectionSummaryParent collectionUpdated) =>
                        {
                            return onFound(collectionUpdated.collection);
                        },
                        mutateRequest: (request) =>
                        {
                            request.Headers.Add("X-API-Key", apiKey);
                            return request;
                        },
                        onFailureWithBody:(statusCode, body) => onFailure(body));
                });
        }
    }
}

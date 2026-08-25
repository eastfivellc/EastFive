using System;
namespace EastFive.Api.Meta.Postman.Resources
{
    public class CollectionSummary
    {
        public Guid id;
        public string name;
        public string owner;
        public DateTime createdAt;
        public DateTime updatedAt;
        public string uid;
        public bool isPulic;
    }

    public class CollectionSummaries
    {
        public CollectionSummary[] collections;
    }

    public class CollectionSummaryParent
    {
        public CollectionSummary collection;
    }
}


//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace BlackBarLabs.Collections.Async
//{
//    public interface IEnumeratorAsync<TDelegate> : IDisposable
//    {
//        Task<bool> MoveNextAsync(TDelegate callback);
//        Task ResetAsync();
//    }

//    public interface IEnumeratorStructAsync<TStruct> : IDisposable
//    {
//        TStruct Current { get; }
//        Task<bool> MoveNextAsync();
//        Task ResetAsync();
//    }
//}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core
{
    public interface IBalanceSessionFactory : IDisposable
    {
        IBalanceSession Create();
    }
}
    

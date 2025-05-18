﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using SmartTask.Bl.Helpers;

namespace SmartTask.Bl.IServices
{
    public interface IPaginatedService<T>
    {
        Task<PaginatedList<T>> GetFiltered(Expression<Func<T, bool>>? filter, int page, int pageSize);
    }
}

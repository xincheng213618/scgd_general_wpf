using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Database
{
    public class BaseTableDao<T> where T : class, IEntity, new()
    {

    }

    public static class BaseTableDaoExtensions
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseTableDaoExtensions));

        #region Private Helpers
        private static bool EnsureConnected()
        {
            if (!MySqlControl.GetInstance().IsConnect)
            {
                log.Warn("数据库未连接, 操作被忽略");
                return false;
            }
            return true;
        }

        private static TResult ExecuteSafe<TResult>(Func<TResult> func, TResult defaultValue)
        {
            try { return func(); }
            catch (Exception ex)
            {
                log.Error(ex);
                return defaultValue;
            }
        }

        private static async Task<TResult> ExecuteSafeAsync<TResult>(Func<Task<TResult>> func, TResult defaultValue)
        {
            try { return await func().ConfigureAwait(false); }
            catch (Exception ex)
            {
                log.Error(ex);
                return defaultValue;
            }
        }

        private static readonly Dictionary<Type, PropertyInfo[]> _propsCache = new();
        private static PropertyInfo[] GetProps(Type t)
        {
            if (!_propsCache.TryGetValue(t, out var props))
            {
                props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                _propsCache[t] = props;
            }
            return props;
        }

        private static bool HasProperty<T>(string name)
        {
            var props = GetProps(typeof(T));
            foreach (var prop in props)
            {
                var sugarAttr = prop.GetCustomAttribute<SugarColumn>();
                var columnName = sugarAttr?.ColumnName ?? prop.Name;
                if (string.Equals(columnName, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static object NormalizeValue(object value)
        {
            if (value == null) return value;
            var tp = value.GetType();
            if (tp.IsEnum) return (int)value;
            if (value is bool b) return b ? 1 : 0;
            return value;
        }
        #endregion

        #region 基础查询
        public static List<T> GetAll<T>(this BaseTableDao<T> dao, int limit = -1) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return new List<T>();
            return ExecuteSafe(() =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                var query = db.Queryable<T>();
                if (limit > 0) query = query.Take(limit);
                return query.ToList();
            }, new List<T>());
        }

        public static Task<List<T>> GetAllAsync<T>(this BaseTableDao<T> dao, int limit = -1, CancellationToken token = default) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return Task.FromResult(new List<T>());
            return ExecuteSafeAsync(async () =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                var query = db.Queryable<T>();
                if (limit > 0) query = query.Take(limit);
                token.ThrowIfCancellationRequested();
                return await query.ToListAsync();
            }, new List<T>());
        }

        public static List<T> GetAllByPid<T>(this BaseTableDao<T> dao, int pid) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return new List<T>();
            return ExecuteSafe(() =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                return db.Queryable<T>().Where("pid = @pid", new { pid }).ToList();
            }, new List<T>());
        }

        public static T? GetById<T>(this BaseTableDao<T> dao, int? id) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return default;
            if (id == null || id <= 0) return default;
            return ExecuteSafe(() =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                return db.Queryable<T>().First(x => x.Id == id) ?? default;
            }, default(T));
        }

        public static Task<T?> GetByIdAsync<T>(this BaseTableDao<T> dao, int? id) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return Task.FromResult<T?>(default);
            if (id == null || id <= 0) return Task.FromResult<T?>(default);
            return ExecuteSafeAsync(async () =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                return await db.Queryable<T>().Where(x => x.Id == id).FirstAsync();
            }, default(T));
        }

        public static List<T> GetAllByBatchId<T>(this BaseTableDao<T> dao, int batchid) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return new List<T>();
            return ExecuteSafe(() =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                return db.Queryable<T>().Where("batch_id = @batchid", new { batchid }).ToList();
            }, new List<T>());
        }

        public static List<T> GetAllByParam<T>(this BaseTableDao<T> dao, Dictionary<string, object> param, int limit = -1) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return new List<T>();
            if (param == null || param.Count == 0) return dao.GetAll(limit);

            return ExecuteSafe(() =>
            {
                var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                var query = db.Queryable<T>();
                foreach (var kv in param)
                {
                    var key = kv.Key;
                    if (!HasProperty<T>(key))
                    {
                        log.Warn($"忽略不存在的列: {key} -> {typeof(T).Name}");
                        continue;
                    }
                    if (kv.Value == null)
                    {
                        query = query.Where($"{key} IS NULL");
                    }
                    else
                    {
                        var value = NormalizeValue(kv.Value);
                        var p = new Dictionary<string, object> { { key, value } };
                        query = query.Where($"{key} = @{key}", p);
                    }
                }
                if (limit > 0)
                {
                    query = query.OrderBy(x => x.Id, OrderByType.Desc).Take(limit);
                }
                return query.ToList();
            }, new List<T>());
        }

        public static Task<List<T>> GetAllByParamAsync<T>(this BaseTableDao<T> dao, Dictionary<string, object> param, int limit = -1, CancellationToken token = default) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return Task.FromResult(new List<T>());
            if (param == null || param.Count == 0) return dao.GetAllAsync(limit, token);

            return ExecuteSafeAsync(async () =>
            {
                var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                var query = db.Queryable<T>();
                foreach (var kv in param)
                {
                    var key = kv.Key;
                    if (!HasProperty<T>(key))
                    {
                        log.Warn($"忽略不存在的列: {key} -> {typeof(T).Name}");
                        continue;
                    }
                    if (kv.Value == null)
                    {
                        query = query.Where($"{key} IS NULL");
                    }
                    else
                    {
                        var value = NormalizeValue(kv.Value);
                        var p = new Dictionary<string, object> { { key, value } };
                        query = query.Where($"{key} = @{key}", p);
                    }
                }
                if (limit > 0)
                {
                    query = query.OrderBy(x => x.Id, OrderByType.Desc).Take(limit);
                }
                token.ThrowIfCancellationRequested();
                return await query.ToListAsync();
            }, new List<T>());
        }

        public static T? GetByParam<T>(this BaseTableDao<T> dao, Dictionary<string, object> param) where T : class, IEntity, new()
        {
            return dao.GetAllByParam(param, 1).FirstOrDefault();
        }

        public static Task<T?> GetByParamAsync<T>(this BaseTableDao<T> dao, Dictionary<string, object> param, CancellationToken token = default) where T : class, IEntity, new()
        {
            return dao.GetAllByParamAsync(param, 1, token).ContinueWith(t => t.Result.FirstOrDefault(), token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        #endregion

        #region 插入/更新
        public static int Save<T>(this BaseTableDao<T> dao, T item) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return -1;
            return ExecuteSafe(() =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                if (item.Id <= 0)
                {
                    var newId = db.Insertable(item).ExecuteReturnIdentity();
                    item.Id = newId;
                    return 1; // 规范：返回受影响行数
                }
                else
                {
                    return db.Updateable(item).Where(x => x.Id == item.Id).ExecuteCommand();
                }
            }, -1);
        }

        public static async Task<int> SaveAsync<T>(this BaseTableDao<T> dao, T item, CancellationToken token = default) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return -1;
            return await ExecuteSafeAsync(async () =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                if (item.Id <= 0)
                {
                    var newId = await db.Insertable(item).ExecuteReturnIdentityAsync();
                    item.Id = newId;
                    return 1;
                }
                else
                {
                    token.ThrowIfCancellationRequested();
                    return await db.Updateable(item).Where(x => x.Id == item.Id).ExecuteCommandAsync();
                }
            }, -1);
        }

        public static int SaveAndReturnId<T>(this BaseTableDao<T> dao, T item) where T : class, IEntity, new()
        {
            var ret = dao.Save(item);
            return ret <= 0 ? -1 : item.Id;
        }

        public static async Task<int> SaveAndReturnIdAsync<T>(this BaseTableDao<T> dao, T item, CancellationToken token = default) where T : class, IEntity, new()
        {
            var ret = await dao.SaveAsync(item, token);
            return ret <= 0 ? -1 : item.Id;
        }
        #endregion

        #region 删除与存在判断
        public static int DeleteById<T>(this BaseTableDao<T> dao, int id) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return -1;
            if (id <= 0) return 0;
            return ExecuteSafe(() =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                return db.Deleteable<T>().Where(x => x.Id == id).ExecuteCommand();
            }, -1);
        }

        public static Task<int> DeleteByIdAsync<T>(this BaseTableDao<T> dao, int id) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return Task.FromResult(-1);
            if (id <= 0) return Task.FromResult(0);
            return ExecuteSafeAsync(async () =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                return await db.Deleteable<T>().Where(x => x.Id == id).ExecuteCommandAsync();
            }, -1);
        }

        public static int Delete<T>(this BaseTableDao<T> dao, Expression<Func<T, bool>> predicate) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return -1;
            return ExecuteSafe(() =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                return db.Deleteable<T>().Where(predicate).ExecuteCommand();
            }, -1);
        }

        public static Task<int> DeleteAsync<T>(this BaseTableDao<T> dao, Expression<Func<T, bool>> predicate) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return Task.FromResult(-1);
            return ExecuteSafeAsync(async () =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                return await db.Deleteable<T>().Where(predicate).ExecuteCommandAsync();
            }, -1);
        }

        public static bool Exists<T>(this BaseTableDao<T> dao, Expression<Func<T, bool>> predicate) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return false;
            return ExecuteSafe(() =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                return db.Queryable<T>().Any(predicate);
            }, false);
        }

        public static Task<bool> ExistsAsync<T>(this BaseTableDao<T> dao, Expression<Func<T, bool>> predicate) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return Task.FromResult(false);
            return ExecuteSafeAsync(async () =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                return await db.Queryable<T>().AnyAsync(predicate);
            }, false);
        }

        public static int Count<T>(this BaseTableDao<T> dao, Expression<Func<T, bool>>? predicate = null) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return 0;
            return ExecuteSafe(() =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                var q = db.Queryable<T>();
                if (predicate != null) q = q.Where(predicate);
                return q.Count();
            }, 0);
        }

        public static Task<int> CountAsync<T>(this BaseTableDao<T> dao, Expression<Func<T, bool>>? predicate = null) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return Task.FromResult(0);
            return ExecuteSafeAsync(async () =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                var q = db.Queryable<T>();
                if (predicate != null) q = q.Where(predicate);
                return await q.CountAsync();
            }, 0);
        }
        #endregion

        #region 分页
        public static (List<T> Items, int Total) GetPaged<T>(this BaseTableDao<T> dao, int pageIndex, int pageSize, Expression<Func<T, bool>>? predicate = null, Expression<Func<T, object>>? orderBy = null, bool desc = true) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return (new List<T>(), 0);
            return ExecuteSafe(() =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                var q = db.Queryable<T>();
                if (predicate != null) q = q.Where(predicate);
                if (orderBy != null)
                {
                    q = desc ? q.OrderBy(orderBy, OrderByType.Desc) : q.OrderBy(orderBy, OrderByType.Asc);
                }
                else
                {
                    q = q.OrderBy(x => x.Id, OrderByType.Desc);
                }
                int total = 0;
                var list = q.ToPageList(pageIndex, pageSize, ref total);
                return (list, total);
            }, (new List<T>(), 0));
        }

        public static Task<(List<T> Items, int Total)> GetPagedAsync<T>(this BaseTableDao<T> dao, int pageIndex, int pageSize, Expression<Func<T, bool>>? predicate = null, Expression<Func<T, object>>? orderBy = null, bool desc = true, CancellationToken token = default) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return Task.FromResult((new List<T>(), 0));
            return ExecuteSafeAsync(async () =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                var q = db.Queryable<T>();
                if (predicate != null) q = q.Where(predicate);
                if (orderBy != null)
                {
                    q = desc ? q.OrderBy(orderBy, OrderByType.Desc) : q.OrderBy(orderBy, OrderByType.Asc);
                }
                else
                {
                    q = q.OrderBy(x => x.Id, OrderByType.Desc);
                }
                token.ThrowIfCancellationRequested();
                RefAsync<int> total = 0;
                var list = await q.ToPageListAsync(pageIndex, pageSize, total);
                return (list, (int)total);
            }, (new List<T>(), 0));
        }
        #endregion

        #region 批量操作
        public static int BulkInsert<T>(this BaseTableDao<T> dao, IEnumerable<T> items) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return -1;
            if (items == null) return 0;
            return ExecuteSafe(() =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

                return db.Insertable(items.ToList()).ExecuteCommand();
            }, -1);
        }

        public static Task<int> BulkInsertAsync<T>(this BaseTableDao<T> dao, IEnumerable<T> items, CancellationToken token = default) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return Task.FromResult(-1);
            if (items == null) return Task.FromResult(0);
            return ExecuteSafeAsync(async () =>
            {
                token.ThrowIfCancellationRequested();
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                return await db.Insertable(items.ToList()).ExecuteCommandAsync();
            }, -1);
        }
        #endregion

        #region 其它
        public static int GetNextAvailableId<T>(this BaseTableDao<T> dao) where T : class, IEntity, new()
        {
            if (!EnsureConnected()) return 1;
            return ExecuteSafe(() =>
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                if (!db.Queryable<T>().Any()) return 1;
                var maxId = db.Queryable<T>().Max(it => it.Id);
                return maxId > 0 ? maxId + 1 : 1;
            }, 1);
        }
        #endregion
    }
}

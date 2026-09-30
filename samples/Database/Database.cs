using Microsoft.Data.Sqlite;
using Objo.Runtime.Abstractions;

namespace Acme.Database;

/// <summary>A typed SQLite query parameter. Values are copied at creation.</summary>
[ObjoExport]
public sealed class Parameter
{
    internal object Value { get; }

    private Parameter(string name, object value)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("A parameter name is required.", nameof(name)) : name;
        Value = value;
    }

    /// <summary>The parameter name, including its @, :, or $ prefix.</summary>
    public string Name { get; }

    /// <summary>Creates a text parameter.</summary>
    public static Parameter Text(string name, string value) => new(name, value);

    /// <summary>Creates a 64-bit integer parameter.</summary>
    public static Parameter Integer(string name, long value) => new(name, value);

    /// <summary>Creates a floating-point parameter.</summary>
    public static Parameter Real(string name, double value) => new(name, value);

    /// <summary>Creates a binary parameter and copies its bytes.</summary>
    public static Parameter Blob(string name, byte[] value) => new(name, value.ToArray());

    /// <summary>Creates a null parameter.</summary>
    public static Parameter Null(string name) => new(name, DBNull.Value);
}

/// <summary>A materialised query result. Dispose it when its rows are no longer needed.</summary>
[ObjoExport]
public sealed class Result : IDisposable
{
    private readonly Row[] _rows;
    private bool _disposed;

    internal Result(IReadOnlyList<Dictionary<string, object?>> rows) =>
        _rows = rows.Select(fields => new Row(this, fields)).ToArray();

    /// <summary>The number of rows.</summary>
    public long Count { get { RequireLive(); return _rows.LongLength; } }

    /// <summary>Returns a row by zero-based index.</summary>
    public Row Item(long index)
    {
        RequireLive();
        if (index < 0 || index >= _rows.LongLength) throw new IndexOutOfRangeException();
        return _rows[index];
    }

    internal void RequireLive()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(Result));
    }

    /// <summary>Releases this result and invalidates retained rows.</summary>
    public void Dispose() => _disposed = true;
}

/// <summary>A typed, materialised row retained by its result.</summary>
[ObjoExport]
public sealed class Row
{
    private readonly Result _owner;
    private readonly IReadOnlyDictionary<string, object?> _fields;

    internal Row(Result owner, IReadOnlyDictionary<string, object?> fields)
    {
        _owner = owner;
        _fields = fields;
    }

    /// <summary>Whether a field contains SQL NULL.</summary>
    public bool IsNull(string name) => Field(name) is null;

    /// <summary>Reads a text field.</summary>
    public string Text(string name) => (string)Field(name)!;

    /// <summary>Reads a 64-bit integer field.</summary>
    public long Integer(string name) => (long)Field(name)!;

    /// <summary>Reads a floating-point field.</summary>
    public double Real(string name) => (double)Field(name)!;

    /// <summary>Reads a binary field and returns an independent copy.</summary>
    public byte[] Blob(string name) => ((byte[])Field(name)!).ToArray();

    private object? Field(string name)
    {
        _owner.RequireLive();
        if (!_fields.TryGetValue(name, out var value)) throw new IndexOutOfRangeException($"Unknown field '{name}'.");
        return value;
    }
}

/// <summary>A typed transaction token owned by one connection.</summary>
[ObjoExport]
public sealed class Transaction
{
    internal Connection Owner { get; }
    internal SqliteTransaction Provider { get; }
    internal bool Active { get; set; } = true;

    internal Transaction(Connection owner, SqliteTransaction provider)
    {
        Owner = owner;
        Provider = provider;
    }

    /// <summary>Whether this transaction can still be committed or rolled back.</summary>
    public bool IsActive => Active;
}

/// <summary>A serial SQLite connection. Database work runs through the host scheduler.</summary>
[ObjoExport]
public sealed class Connection : IDisposable
{
    private readonly SqliteConnection _provider;
    private readonly string _path;
    private readonly object _cancelLock = new();
    private readonly ManualResetEventSlim _cancelSignal = new(initialState: false);
    private SqliteCommand? _currentCommand;
    private bool _cancelRequested;
    private bool _disposed;
    private Transaction? _transaction;

    private Connection(SqliteConnection provider, string path)
    {
        _provider = provider;
        _path = path;
    }

    /// <summary>Opens or creates one private SQLite file.</summary>
    [ObjoAsync]
    public static Connection Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A database path is required.", nameof(path));
        var fullPath = Path.GetFullPath(path);
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        };
        var provider = new SqliteConnection(builder.ToString());
        try
        {
            provider.Open();
            return new Connection(provider, fullPath);
        }
        catch
        {
            provider.Dispose();
            throw;
        }
    }

    /// <summary>Executes a parameterised statement and returns affected rows.</summary>
    [ObjoAsync]
    public long Execute(string sql, Parameter[] parameters)
    {
        using var command = Prepare(sql, parameters);
        return Run(command, () => command.ExecuteNonQuery());
    }

    /// <summary>Executes a parameterised query and returns materialised rows.</summary>
    [ObjoAsync]
    public Result Query(string sql, Parameter[] parameters)
    {
        using var command = Prepare(sql, parameters);
        return Run(command, () =>
        {
            using var reader = command.ExecuteReader();
            var rows = new List<Dictionary<string, object?>>();
            while (reader.Read())
            {
                var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    fields.Add(reader.GetName(i), value is DBNull ? null : value is byte[] bytes ? bytes.ToArray() : value);
                }
                rows.Add(fields);
            }
            return new Result(rows);
        });
    }

    /// <summary>Starts a transaction on this connection.</summary>
    [ObjoAsync]
    public Transaction BeginTransaction()
    {
        RequireLive();
        if (_transaction is not null) throw new InvalidOperationException("A transaction is already active.");
        _transaction = new Transaction(this, _provider.BeginTransaction());
        return _transaction;
    }

    /// <summary>Commits this connection's active transaction.</summary>
    [ObjoAsync]
    public void Commit(Transaction transaction)
    {
        RequireTransaction(transaction);
        transaction.Provider.Commit();
        EndTransaction(transaction);
    }

    /// <summary>Rolls back this connection's active transaction.</summary>
    [ObjoAsync]
    public void Rollback(Transaction transaction)
    {
        RequireTransaction(transaction);
        transaction.Provider.Rollback();
        EndTransaction(transaction);
    }

    /// <summary>Requests cancellation of the current or next pending operation.</summary>
    [ObjoSafeWhilePending]
    public void RequestCancel()
    {
        lock (_cancelLock)
        {
            _cancelRequested = true;
            _cancelSignal.Set();
            _currentCommand?.Cancel();
        }
    }

    /// <summary>Closes this connection. Pending operations must finish first.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_transaction is { } transaction)
        {
            transaction.Provider.Dispose();
            transaction.Active = false;
            _transaction = null;
        }
        _provider.Dispose();
        _cancelSignal.Dispose();
    }

    private SqliteCommand Prepare(string sql, Parameter[] parameters)
    {
        RequireLive();
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL is required.", nameof(sql));
        ArgumentNullException.ThrowIfNull(parameters);
        var command = _provider.CreateCommand();
        try
        {
            command.CommandText = sql;
            command.Transaction = _transaction?.Provider;
            foreach (var parameter in parameters)
                command.Parameters.AddWithValue(parameter.Name, parameter.Value);
            return command;
        }
        catch
        {
            command.Dispose();
            throw;
        }
    }

    private T Run<T>(SqliteCommand command, Func<T> work)
    {
        lock (_cancelLock) _currentCommand = command;
        try
        {
            lock (_cancelLock)
                if (_cancelRequested) throw new OperationCanceledException("The database operation was cancelled.");
            return work();
        }
        finally
        {
            lock (_cancelLock)
            {
                _currentCommand = null;
                _cancelRequested = false;
                _cancelSignal.Reset();
            }
        }
    }

    private void RequireTransaction(Transaction transaction)
    {
        RequireLive();
        if (!ReferenceEquals(transaction.Owner, this) || !ReferenceEquals(_transaction, transaction) || !transaction.Active)
            throw new InvalidOperationException("The transaction is not active on this connection.");
    }

    private void EndTransaction(Transaction transaction)
    {
        transaction.Provider.Dispose();
        transaction.Active = false;
        _transaction = null;
    }

    private void RequireLive()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(Connection));
    }
}

namespace Celeste.Mod.MappingUtils;

public readonly struct OneOf<T1, T2>
{
    private readonly T1 _first;
    private readonly T2 _second;
    private readonly bool _isOne;

    public OneOf(T1 first)
    {
        _first = first;
        _second = default!;
        _isOne = true;
    }

    public OneOf(T2 second)
    {
        _first = default!;
        _second = second;
    }

    public T1? AsT1() => _isOne ? _first : default;
    public T2? AsT2() => _isOne ? default : _second;

    public T Match<T>(Func<T1, T> first, Func<T2, T> second)
    {
        if (_isOne)
            return first(_first);

        return second(_second);
    }
}

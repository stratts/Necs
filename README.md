# Necs

Necs is an **Entity-Component-System** framework for C#. It has 3 main goals - ease of use, efficiency, and flexibility.



## Ease of Use

Necs aims to feel as natural as possible to use, allowing traditional object-oriented concepts without sacrificing performance. 

Hierarchies are supported:

```c#
var ecs = new EcsContext();
    
var e = new Entity();
e.AddComponent(new Position() { X = 10, Y = 20 });

var child = new Entity();
e.AddChild(child);

ecs.AddEntity(e);
```

As is inheritance:

```c#
class Player : Entity 
{
    public Player() 
    {
        AddComponent(new Health() { Amount = 100 });
    }
}
```

Components can be either classes or structs - there is no restriction there.



## Efficiency

Components are stored in contiguous arrays in memory, exploiting the cache behaviour of modern CPUs and allowing iteration code to run as quickly as possible. 

For example, Necs can iterate over 1 million components in just over 1 ms:

```c#
ecs.Query<Component>((ref Component c) => c.Value += 1);
```

| Method       | N    |       Mean |    Error |   StdDev |
| ------------ | ---- | ---------: | -------: | -------: |
| ActionSingle | 100k |   109.9 us |  1.92 us |  2.56 us |
| ActionSingle | 250k |   275.4 us |  5.32 us |  5.46 us |
| ActionSingle | 1M   | 1,147.6 us | 22.77 us | 55.43 us |

Necs also provides direct access to the span of components, avoiding delegate overhead and allowing iteration of 1 million components in less than 0.2 ms (!)

```c#
var components = ecs.GetSpan<Component>();
foreach (ref var c in components) 
    c.Value += 1;
```

| Method | N    |      Mean |    Error |   StdDev |
| ------ | ---- | --------: | -------: | -------: |
| Span   | 100k |  18.30 us | 0.175 us | 0.164 us |
| Span   | 250k |  45.69 us | 0.375 us | 0.332 us |
| Span   | 1M   | 180.17 us | 1.067 us | 0.946 us |

Compared to a normal architecture, where data is stored within each entity class, performance is anywhere from 3-10x faster:

```c#
foreach (var entity in Entities) 
    entity.X += 1;
```

| Method         | N    |        Mean |     Error |   StdDev |
| -------------- | ---- | ----------: | --------: | -------: |
| EntityPosition | 100k |    337.8 us |   2.38 us |  2.22 us |
| EntityPosition | 250k |  2,031.3 us |  21.39 us | 18.96 us |
| EntityPosition | 1M   | 11,900.9 us | 113.89 us | 88.92 us |



## Flexibility

Necs tries to make as few assumptions as possible about how systems should work. Ultimately, a system is just a function that accepts the ECS context (or 'world') as an argument. Beyond this, it can do whatever it likes. 

For example, a system that runs a single query:

```c#
var ecs = new EcsContext();

ecs.AddSystem(ctx => {
    ctx.Query<Component>((ref Component c) => c.Value += 1);
});
```

Or a system that runs a single query, then a query over multiple components:

```c#
ecs.AddSystem(ctx => {
    ctx.Query<Component>((ref Component c) => c.Value += 1);

    ctx.Query<Component, Position>((ref Component c, ref Position p) => {
        p.X += c.Value;
    });
});
```

There is also an interface provided that streamlines the process of registering a class as a system:

```c#
class MySimpleSystem : IComponentSystem 
{
    public void Process(EcsContext ecs) 
    {
    	ecs.Query<Component>((ref Component c) => c.Value += 1);
    }
}

...
    
ecs.AddSystem(new MySimpleSystem());
```


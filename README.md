# Byron

Welcome to the Byron language repository. This readme serves as a motivation statement and philosophical cornerstone of a systems programming language that I feel that I would enjoy using.

## Motivation

Byron is a systems language with explicit manual control and compile time resource accountability. This is a personal project to explore the nuts and bolts of languages and compilers. I'm most at home in C#, but want to get to know systems programming. I've explored both Rust and Zig and feel more at home in the Zig space, sometimes finding myself wishing that both had features from each other.

The language idea arose from the idea of implementing a garbage-collecting allocator in Zig. What would that look like? What does a language  minimally require to support GC. Do absolutely you need a runtime? This evolved to the idea of a language with a bolt-on, importable runtime in a primarily systems level programming language. Byron as a name stems from this idea idea of "Bring your own Runtime". The idea was then superceded by another - safer memory management without GC. Thus was born the [obligation](#the-obligation-system) concept.

First of all, I'd like to describe what I wish such a language looked like, starting with the areas I wish were different in existing languages.

### Rust

- `panic!()` existing alongside `Result<T,E>`. The Cloudflare outage of 2025-11-18 resulted from a panic on `unwrap()`. I wish that it didn't rely on documentation to not call on functions that can panic, but that it was not possible to do so. Crates can also decide that a situation is fatal and should panic, even when I as a developer think it should be recoverable, leading the developer to now have to handle both results and also panics with `catch_unwind`. 
- `Rc<RefCell<T>>` for shared mutability is reached for so often that I think that my own language would be willing to sacrifice the guarantees that the borrow checker provides in order to simplify the lanaguage (and compiler). 
  - Artifacts like `PhantomData` as a bridge between safe and unsafe rust to satisfy the borrow checker indicate a complexity that a lower level language may wish to avoid.
  - `<'a>` lifetime annotations leak into the entire call stack, for example `impl<'a> Trait<'a>`.
  - Method chaining mutable functions is painful.

I understand that there are reasons for each of these design choices, and the safety that the compiler offers is incredibly strong. 

Byron wants 
- _Some_ of the biggest value adds of rusts compile time memory safety, without the complexity of the full borrow checker
- A much stricter adherence to the `Result<T, E>` pattern

### Zig

- `anytype` for comptime functions is awkward.
- No closures or inline lambdas.
- It's possible to forget to `deinit`, or `deinit` with the wrong allocator. (I get that this a fundemental skill issue of systems programming)

I actually really like zig and will likely focus on it as my main systems programming language. The Byron project is looking at the possibility of adding compile time resource accounting and the kinds of compromises that are required

Byron wants 

- Zig's explicit allocator model with a compile time obligation layer on top that guarantee that we free with the correct allocator.
- Zigs philosophy of no hidden control flow, and no hidden costs.

---

## Language Goals

- Strongly typed
- Explicit control flow
    - Results only
        - Pre-allocated results for resource exhaustion. 
        - No userspace `panic`s. 
        - Handling `Error`s is compile time enforced.
    - Abort on physical impossibilities only (stack overflow, CPU exception).
- Pay for what you use — no hidden costs, no hidden control flow.
- Manual memory allocation that is harder to leak, double free, and misuse after ownership transfer than both Zig and C, with compile time enforcement where feasible.
- No `null` (outside of interop).
- Immutability by default.
- C-adjacent syntax — TS/C#/Zig flavour.
- No runtime
- A single reasoning system for compile time resource accounting

## The Obligation System

An **obligation** is a **compile** time requirement that must be resolved exactly once on every code path. Examples include:

- **Initial scope**
    - memory management
    - results / error handling

- **Much later**
    - database connections
    - locks
    - streams
    - channels 
    - user defined

Like in rust, there is the idea of an Owner. This is codified in the type system by the `Owned<T>` type. The carrier of the instance of `Owned<T>`  holds obligation authority to free the value.

You cannot resolve the `free` obligation on an instance upon which you do not hold obligation authority, i.e. if you do not have the `Owned<T>` instance. If you hold obligation authority, you must ensure that the obligation is resolved. See [Ownership Types](#ownership-types) for more information on ownership and obligation authority.

### The `free` obligation

`Owned<T>` carries an obligation to call `free`

### The Result\<T, E\> obligation

`Result<T. E>` is an obligation with two resolvers — the `Ok` branch and the `Error` branch. All potential errors must be handled on every code path.

### How Obligations Are Resolved

// todo we need to fill in all of the result requirements here too, or strip them out in preference of a results section

- Returning the bound instance, transferring the bound obligations to the caller
- `give`ing the bound instance to a function that is willing to accept obligation authority (denoted by a `take Owned<T>` argument), transferring bound obligations to the receiver.
- Calling the required discharge function
- (In the case of an error) Handling the `Result` `error` path

#### Calling the annoted discharge function

```
struct  TransferringAllocator {
    @obligates([.free])
    fn alloc<T>(self &var TransferringAllocator): Result<Owned<T>> {...}
}

fn foo(allocator: &var TransferringAllocator): Result<void> {
    const myBar = allocatoralloc<Bar>()?;

    // Note that the obligation is to call .free on the myBar instance
    myBar.free();
    Return Ok;
}
```

#### Returning the instance

```
struct TransferringAllocator {
    @obligates([.free])
    fn alloc<T>(self &var TransferringAllocator): Result<Owned<T>> {...}
}

fn foo(allocator: &var TransferringAllocator): Result<Owned<Bar>> {
    const myBar = allocatoralloc<Bar>()?;

    // ...

    return myBar;
}
```

#### `Give`ing the the instance to a function prepared to `take` ownership

```
struct TransferringAllocator {
    @obligates([.free])
    fn alloc<T>(self &var TransferringAllocator): Result<Owned<T>> {...}
}

fn receive(take myBar: Owned<Bar>): void {
    myBar.free();
}

fn foo(allocator: &var TransferringAllocator): void {
    const myBar = allocator.alloc<Bar>() onerror return;

    // ...

    receive(give myBar);
}

```

#### Early return error propogation with `?`

```
struct TransferringAllocator {
    @obligates([.free])
    fn alloc<T>(self &var TransferringAllocator): Result<Owned<T>> {...}
}

fn foo(allocator: &var TransferringAllocator): Result<void> {
    const myBar = allocator.alloc<Bar>()?
    myBar.free();
    Return Ok;
}

```

#### Handling the error with `onerror` 
```

struct TransferringAllocator {
    @obligates([.free])
    fn alloc<T>(self &var TransferringAllocator): Result<Owned<T>> {...}
}

fn receive(take myBar: Owned<Bar>): void {
    myBar.free();
}

fn foo(allocator: &var TransferringAllocator): void {
    const myBar = allocator.alloc<Bar>() onerror { 
        return;
    };
    myBar.free();
}
```

### Compilation failures

Let's assume the following struct
```
struct TransferringAllocator {
    @obligates([.free])
    fn alloc<T>(self &var TransferringAllocator): Result<Owned<T>> {...}
}
```
#### Not resolving or returning the insnace

```
fn foo(allocator: &var TransferringAllocator): Result<void> {

    const myBar = allocator.alloc<Bar>()?;
    // myBar.free(); // Did not resolve obligation or return the instance
    Return Ok;

}
```

#### Trying to resolve the obligation more than once

```
fn foo(allocator: &var TransferringAllocator): Result<void> {

    const myBar = allocator.alloc<Bar>()?;
    myBar.free();
    myBar.free(); // Tried to resolve obligation a second time
    Return Ok;
}
```

### Not resolving in all execution paths

```
fn foo(allocator: &var TransferringAllocator): Result<void> {
    let myBool = false;

    const myBar = allocator.alloc<Bar>()?;
    if(myBool) {
        myBar.free();
    } else {
        // Did not resolve obligation in this execution path
    }
    Return Ok;
}
```

#### Not handling the `Error` path

```
fn foo(allocator: &var TransferringAllocator): Result<void> {
    const myBar = allocator.alloc<Bar>();
    myBar.free(); // Did not handle the Error path of the result
    Return Ok;
}
```

## Ownership Transfer - the `give` and `take` keywords

`give` is a call-site keyword. `take` is a receiver keyword — used in parameter declarations as to denote that the function is designed to take ownership, and at call sites when accepting obligation authority from a return value or move.

```
give    // caller surrenders ownership into a function argument
take    // caller accepts the ownership of a returned instance from a call or a move
```

### Give

```
fn example(allocator: &TransferringAllocator): Result<void> {
    let myValue = take allocator.alloc<MyType>()?;
    consume(give myValue); 
    Return Ok;
}

fn consume(myValue: take Owned<MyType>): void { 
    consume(give myValue);
}

fn example(myValue: &var MyType): void {
    consume(give myValue);                     // COMPILE ERROR: No obligation authority
}
```

### Take


You are required to `take` ownership when a function expects to return an `Owned<T>` or when accepting a moved binding

#### Taking ownership of a returned value

```
let myValue = take allocator.alloc<MyType>()?;              // correct
```

```
var node = allocator.alloc<MyType>()?;                      // COMPILE ERROR: Owned<T> must be taken because allocator.alloc<T> returns Result<Owned<T>>
```

#### When moving

```
var myValue = take allocator.alloc<MyType>()?;    
var moved = take myValue;                       // correct. moved is now live and myValue is inaccessible
``` 

```
var myValue = take allocator.alloc<MyType>()?;    
var moved = myValue;                             // COMPILE ERROR: Move must be taken
```

```
var myValue = take allocator.alloc<MyType>()?;    
var moved = take myValue;                        // COMPILE ERROR: myValue is inaccessibly having already been moved
var secondMove = take myValue
```

#### Deconstruction

Every element of a deconstructed object must be must be `take`n
```
let (x, y, z) = take myPoint;       // correct
```
```
let (x, y, z) = myPoint;            // COMPILE ERROR: Move must be taken
```

Every element of a deconstructed object must be must be `take`n

```
let (x, y, z) = take myPoint;
let (a, b, c) = take (x, y, x);      // correct
```

```
let (x, y, z) = take myPoint; 
let (a, b, c) = take (x, y, x)      // z was not taken, x was taken twice
```

## Allocators

Byron has two allocator interfaces, that either give, or retain, memory ownership, depending on your needs

### TransferringAllocator

Returns `Owned<T>`. The caller takes the obligation and is responsible for eventually freeing the memory. A General Purpose Allocator would be an example.

```
interface TransferringAllocator {
    fn alloc<T>(self: &var Self): Result<Owned<T>>,
    fn free<T>(self: &var Self, take value: Owned<T>): Result<void>,
}
```

```
var gpa = take GeneralPurposeAllocator.init()?;
var myBar = take gpa.alloc<Bar>()?;
myBar.free();
gpa.deinit();
```

### RetainingAllocator

Returns `&var T`. The allocator retains the `Owned<T>` and thereby all memory responsibility. Resources are freed when the allocator is deinited with no individual resolution required or even possible. An ArenaAllocator would be an example

```
interface RetainingAllocator {
    fn alloc<T>(self: &var Self): Result<&var T>,
}
```

```
var scoped = take ArenaAllocator.init()?;
var myBar = take scoped.alloc<Bar>()?;
scoped.deinit();                                  // myBar is now freed
```

### References

The `let` keyword declares immutability: `let x = 5;`.
The `var` keyword declares mutability: `var x = 5; x = 6;`

```
Owned<T>    // Denotes an owned instance og a T with obligation to free memory. Must be `take`n
&T          // Denotes an immutable reference to a T
&var T      // denotes a mutable reference to a T
```

### Primitive types

 We will of course be adding in smaller versions of all number types. We are starting with these big number types if only so that I don't have to think about the smaller ones for now. 

- `bool`
- `byte`
- `char`
- `i64`
- `u64`
- `f64`
- `Array<T>`
- `void`


### Ergonomics
I've tried to consider the ergeoomics of working with such explicitness, because boiler plate is inevitable when we want every cost or every decision to be laid bare in front of us. To that end, here are some initial ideas of how to make the idea workable. Many of these will be deferred, however.

- `?` to early return an error from a called function, e.g. `let a = i32.TryParse("gdgd")?;`
- A short hand default value to handle errors e.g. `let a = i32.TryParse("gdgd") onerror 0;`
- `let` and `var` for (im)mutability
- Tagged unions with exhaustive pattern matching
- `Ok` token for the success case of `Result<void>`
- Optional type inferences
- destructuring with `take` semantics in order to take ownership of its constituent parts, e.g. `let (x, y, z) = take myPoint3d;`
- `0..5` and `0..=5` for exclusive/inclusive upper bounds (deferred)
- `defer` and `errordefer` to place fulfilment of the obligation alongside its creations (deferred)
- `Option<T>` with `if let Some(myValue) = someOption` (deferred)

## Where next?

I have a goal to be able to implement the following programs in this language:

- The trivial start and immediately return 0 
- Print "Hello World" and return 0 
- Allocate, deallocate, and return 0 
- Accept user input N, print the fibonachi sequence up to N, and return 0

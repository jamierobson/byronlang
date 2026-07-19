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
- _Some_ of the biggest value adds of rusts compile time memory double-free, use-after-free, and memory leak prevention, without the complexity of the full borrow checker (e.g. not looking for rusts aliasing model)
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
- No managed runtime
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

### Result\<T, E\>

`Result<T. E>` carries an obligation to handle the error from an error branch, as well as any ownership obligation from the success branch. All potential errors must be handled on every code path.

### How Obligations Are Resolved

- Returning the bound instance, transferring the bound obligations to the caller
- `give`ing the bound instance to a function that is willing to accept obligation authority (denoted by a `take Owned<T>` argument), transferring bound obligations to the receiver.
- Calling the required discharge function
- (In the case of an error) Handling the `Result` `error` path

### Examples

Assume, in the following examples, the existance of
```
interface TransferringAllocator {
    @obligates([.fill, .release])
    fn alloc<T>(self: &var Self): Result<Uninitialized<Owned<T>>, OutOfMemoryError>,
}

struct Uninitialized<T> {
    //
}

implement Uninitialized<T> {
    @obligates([.free])
    fn fill(value: T): Owned<T> {...}
    fn release() {...}
}

struct GeneralPurposeAllocator {
}

implement TransferringAllocator for GeneralPurposeAllocator {
    @obligates([.fill, .release])
    fn alloc<T>(self: &var Self): Result<Uninitialized<Owned<T>>, OutOfMemoryError> {...}
}

struct Owned<T> {

}

implement Owned<T> {
    free(&self: Owned<Self>)
}

```
#### Calling the annoted discharge function

```
fn foo(allocator: &var TransferringAllocator): Result<void> {

    const myMemory = take allocator.alloc<Bar>()?; // Note that the obligation is to call .fill on the myMemory instance
    let myBar = take myMemory.fill(MyBar{member1: 1, member2: 42}); // Note that the obligation is to call .free on the myBar instance
    myBar.free();
    Return Ok;
}
```

#### Returning the instance

```
fn foo(allocator: &var TransferringAllocator): Result<Owned<Bar>> {
    
    const myMemory = take allocator.alloc<Bar>()?;
    let myBar = take myMemory.fill(MyBar{member1: 1, member2: 42});

    // ...

    return myBar;
}
```

#### `Give`ing the the instance to a function prepared to `take` ownership

```
fn receive(take myBar: Owned<Bar>): void {

    ...
    myBar.free();
}

fn foo(allocator: &var TransferringAllocator): void {

    const myMemory = take allocator.alloc<Bar>()?;
    let myBar = take myMemory.fill(MyBar{member1: 1, member2: 42});

    receive(give myBar);
}

```

#### Early return error propogation with `?`

```

fn foo(allocator: &var TransferringAllocator): Result<void> {

    const myMemory = take allocator.alloc<Bar>()?;
    let myBar = take myMemory.fill(MyBar{member1: 1, member2: 42});

    myBar.free();
    Return Ok;
}

```

#### Handling the error with `onerror` 
```
fn foo(allocator: &var TransferringAllocator): Result<Owned<Bar>> {

    const myMemory = take allocator.alloc<Bar>() onerror e { 
        logError(e)
        return e;
    };;

    let myBar = take myMemory.fill(MyBar{member1: 1, member2: 42});

    const myBar = take allocator.alloc<Bar>()
    return myBar;
}
```

```
fn foo(): Option<i32> {
    const myBar = someIntFunctionThatCanError() onerror 0;
    Return Some(myBar)
}
```


### Compilation failures

#### Not resolving or returning the insnace

```
fn foo(allocator: &var TransferringAllocator): Result<void> {

    const myMemory = take allocator.alloc<Bar>()?;
    // let myBar = take myMemory.fill(MyBar{member1: 1, member2: 42}); // Did not resolve obligation or return the instance
    Return Ok;

}
```

#### Trying to resolve the obligation more than once

```
fn foo(allocator: &var TransferringAllocator): Result<void> {

    const myMemory = take allocator.alloc<Bar>()?;
    let myBar = take myMemory.fill(MyBar{member1: 1, member2: 42});
    myBar.free();
    myBar.free(); // Tried to resolve obligation a second time
    Return Ok;
}
```

### Not resolving in all execution paths

```
fn foo(allocator: &var TransferringAllocator): Result<void> {

    let myBool = false;
    const myMemory = take allocator.alloc<Bar>()?;
    if(myBool) {
        myBar.release();
    } else {
        // Did not resolve obligation in this execution path
    }
    Return Ok;
}
```

#### Not handling the `Error` path

```
fn foo(allocator: &var TransferringAllocator): Result<void> {

    const myMemory = take allocator.alloc<Bar>();
    
    // Did not handle the Error path of the myMemory result, note the missing `?` from the line above
    let myBar = take myMemory.fill(MyBar{member1: 1, member2: 42});
    myBar.free(); 
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
    let myMemory = take allocator.alloc<MyType>()? 
    let myValue = take myMemory.fill(MyType {i: 1});
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
let myMemory = take allocator.alloc<MyType>()?;              // correct
```

```
var myMemory = allocator.alloc<MyType>()?;                      // COMPILE ERROR: Uninitialized<T> must be taken because allocator.alloc<T> returns Result<Uninitialized<T>>
```

#### Closing mutability
```
var myInt: i32 = 42;
myInt = 41;
let nowImmutable = take myInt; // correct. myInt is inaccessible, and the backing value is now immutable.
```

#### When moving

```
var myMemory = allocator.alloc<MyType>()?;
let myValue = take myMemory.fill(MyType {i: 1});  
var moved = take myValue;                       // correct. moved is now live and myValue is inaccessible
``` 

```
var myMemory = allocator.alloc<MyType>()?;
let myValue = take myMemory.fill(MyType {i: 1});
var moved = myValue;                             // COMPILE ERROR: Move must be taken
```

```

var myMemory = allocator.alloc<MyType>()?;
let myValue = take myMemory.fill(MyType {i: 1});
var moved = take myValue;                        
var secondMove = take myValue // COMPILE ERROR: myValue is inaccessibly having already been moved
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

Byron has two allocator interfaces, that either give, or retain, memory ownership, depending on your needs.

Each presents an `alloc` function, that returns a type-safe block of uninitialized memory, which you are obligated to `fill` with a value. 

### TransferringAllocator

The `alloc` function returns `Uninitialized<T>` which represents an empty block of memory, that the caller is responsible for, large enough to place an `Owned<T>` A value is placed by calling the `fill` function, returning the `Owned<T>`. Calling `.fill` is an obligation. The caller takes ownership of the `Owned<T>` instance, including the `free` obligation and is responsible for eventually freeing the memory. A General Purpose Allocator would be an example.

```
interface TransferringAllocator {
    @obligates([.fill, .release])
    fn alloc<T>(self: &var Self): Result<Uninitialized<T>>,
}

struct Uninitialized<T> {
    allocator: &var TransferringAllocator
}

implement struct Uninitialized<T> {
    @obligates([.free])
    fill(self: Owned<Self>, value: T): Owned<T> {...},
    release(self: Owned<Self>): void {...}
}
```

```
var gpa = take GeneralPurposeAllocator.init()?;
var allocator = gpa.allocator();
var memory = take allocator.alloc<Bar>()?;
let myBar = take memory.fill(Bar{a: 42});
myBar.free();
gpa.deinit();
```

### RetainingAllocator

The `alloc` function returns `MemoryLease<T>` which represents an empty block of memory, that the allocator retains responsibility for, large enough to place a `T` A value is placed by calling the `fill` function, returning `&var T`. Calling `.fill` is an obligation. The allocator retainns ownership of the `Owned<T>` instance, including the `free` obligation and is responsible for eventually freeing the memory. An Arena Allocator would be an example.

```
interface RetainingAllocator {
    @obligates([.fill, .release])
    fn alloc<T>(self: &var Self): Result<MemoryLease<T>>,
}

struct MemoryLease<T> {
    allocator: &var TransferringAllocator
}

implement struct Uninitialized<T> {
    fill(self: Owned<Self>, value: T): &var T {...},
    release(self: Owned<Self>): void {...}
}
```

```
var arena = take ArenaAllocator.init()?;
var allocator = arena.allocator();
var myLease = take allocator.alloc<Bar>()?;
let myBar = myLease.fill(Bar{a: 42});
allocator.deinit();                                  // myBar is now freed
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
I've tried to consider the ergeoomics of working with such explicitness, because boiler plate is inevitable when we want every cost or every decision to be laid bare in front of us. To that end, here are some initial ideas of how to make the idea workable. Many of these will be deferred, however. As an aside, any sugar would have to be shorthad that lowers to code that could otherwise be hand written

- `?` to early return an error from a called function, e.g. `let a = i32.TryParse("gdgd")?;`
- A short hand default value to handle errors e.g. `let a = i32.TryParse("gdgd") onerror 0;`
- `let` and `var` for (im)mutability
- Tagged unions with exhaustive pattern matching
- `Ok` token for the success case of `Result<void>`
- Optional type inference
- destructuring with `take` semantics in order to take ownership of its constituent parts, e.g. `let (x, y, z) = take myPoint3d;`
- `0..5` and `0..=5` range syntax for exclusive/inclusive upper bounds (deferred)
- `defer` and `errordefer` to place fulfilment of the obligation alongside its creations (deferred)
- `Option<T>` with `if let Some(myValue) = someOption` (deferred)

## Where next?

I'm very happy to accept that the compiler will be rejecting a lot of valid programs based on its inability to prove complex programs. It's a personal project, and I want to get good value from my time, getting as much as possible out of the limited time I have. Over time, if we ever make any progress, I'd like to make it more robust and smart.

I have a goal to be able to implement the following programs in this language:

- The trivial start and immediately return 0 
- Invoke some function, do some arithmetic, and return the result
- Print "Hello World"
- Allocate and deallocate
- Accept user input N, print the fibonachi sequence up to N
- Implement a doubly linked list
- A sudoku solver

If we can do that, we're making enough progress to be worth pushing all the way to self hosted, I think. 

# Getting Started

- Install [clang](https://releases.llvm.org/download.html)
- Install [dotnet 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
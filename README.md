# Byron

Welcome to the Byron language repository. Byron is a systems programming language currently in the conceptualization stage, currently exploring the feasability of the wishlist before jumping in to trying to implementing... something. (This kind of thing is way beyond my curent knowledge)

## Motivation 
This is a personal project in order to explore the nuts and bolts of languages and compilers. I currently am most familiar with c#, and would like to get to know systems programming. To that end, I've explored rust and zig, and feel more at home in the zig space, but wishing that both had features from each other. I'd therefore like to understand, what would be a middle ground that I personally would enjoy writing with.

The language as described kind of sounds, and feels like a marriage of rust and zig. It's not trying to offer the same safety as rust, but wants to explore the idea of attaching resource accountancy to a zig-like language with explicit resource management, and seeing if we can eliminate the most common classes of problems.

Likely doomed to fail to even get off the ground, I should at least learn something!

### Why not Rust?

- I find it awkward that rust evangelizes `Result<T,E>` (I love `Result`) types but [still allows](https://effective-rust.com/panic.html) you to `panic!()` on failures that in many cases really were recoverable, or that should have been results. [The cloudflare outage of 2025-11-18](https://blog.cloudflare.com/18-november-2025-outage/) resulted in a panic by calling `unwrap()`. I wish that there was no such thing as a panic, at all, outside of physical impossibilities. 
- I dislike how often things turn in to an `Rc<RefCell<T>>` for shared mutability - I feel like it's a tool so often reached for that this perhaps should be represented differently than describing runtime responsibility through these types.
- Things like `PhantomData` feel very clumsy
- Method chaining mutable functions is hellish
- Recompiling referenced crates in your builds adding to build times.

#### The petty
- I_am_not_keen_on_snake_case
- I find shorthand annoying when it leads to extra mental load to work out what is going on. Some abbreviations like `fn` are pretty ubiquitous, but `Vec` for Vector, `&str` for a string slice, `mut` for mutable... I wish they were given their actual names! 
- `<'a>` annotations that leak into the entire call stack. `impl<'a> Trait<'a>` feels particularly egregious. In fact, lifeftimes as generic arguments feels... I don't love it.

### Why not Zig?

I actually really like zig, and mostly just wanted to explore what if a similar language added some more safety at the cost of some flexibility. The list of things I genuinely wish were different are

- I wish that function arguments didn't  require `anytype` for `comptime` functions
- I wish we had closures and linline functions / lambdas
- I wish that accepting the cost of dynamic dispatch was achieved through other mechanisms other than making interfaces cumbersome.

#### The petty
- I wish that there was a neater syntax for an inclusive upper bound of a range

## Language goals
Getting slightly ahead of ourselves, the systems language I wish existed has the best of both of these languages. (Don't get me wrong, I'm very happy to keep using zig as my systems programming language, i'd just enjoy it even more with a couple of additional considerations). I don't intend for this language to be a zig knock off, but there will certainly be similarities. (Please don't sue me)

To that end, here's my thoughts of what I wish a language was:

- Strongly typed
- Explicit control flow: no exceptions, no panics. Results only. Pre allocated results for resource exhaustion. Abort on physical impossibilities only, e.g. stack overflow, CPU exception.
- Results that are compile time enforced handled.
- Pay for what you use.
- Manual memory allocation that is harder to leak, double free, and accidentally misuse after ownership transfer than both Zig and C — with compile-time enforcement where feasible.
- No `null` (outside of interop)
- Immutability by default
- Functions, structs, unions, (eventually traits) as equal citizens. (Dynamic dispatch to be addressed later)
- c adjacent syntax

## Envisioned features
### The Obligation Tracker
The obligation tracker is a resource accountant for obligations

An Obligation is a compile time concept that codifies a requirement to perform an action, for example to release memory again, or to handle an error. Consider obligations to be linear, in that you can do as you like with the instance that carries the obligation, but you consume the obligation precisely once when you fulfil it. 

Memory is allocated though allocators (see zig for the inspiration). Every allocation and every initialization creates an obligation to be fulfilled. Every every error is an obligation to be fulfilled. A Result requires fulfilment of both the success case and the error case. At compile time, the obligation tracker (similarly to the rust borrow checker, but hopefully simpler and more lenient) ensures that all obligations are fulfilled exactly once, in every execution branch.

An obligation is fulfilled by: 
- Actively calling the fulfilment function for the obligation (`deinit`, `free`)
- Returning the instance with the attached obligation, or the error, the caller
- In the case of allocations, the obligation can be created from an "Owning" type of allocator, and the obligation is fulfilled by `deinit`ing the allocator, which then calls that function on all owned instances. 
- In the case of errors, handling the error explicitely
- `give`ing the instance with the attached obligation to a function prepared to `take` (e.g. `Append` to a list, or to give to a thread)

Instances are returned up the stack, or moved. By default, instances are borrowed with references but can be moved with `give` and `take` semantics, similar to rusts move, but with explicit syntax. It is a compilation error to try to fulfil an obligation for a type you down own. E.g., it would be a compilation failure to try to `deinit` an allocator you were provided, unless it was `give`n to you.

### Safety features
- `malloc` and `free` allowed in allocator implementations only
- `null`, null pointers, pointer arithmetic, catch-unwind and some way to release tracking of obligations inside of ffi layers, (essentially, enough to be able to interop with C). These would be exoected to return byron safe types in all instances
- Everything must be handled when destructuring
- Every potential obligation must be fulfilled precisely once in every execution path
- A small overhead for pre-allocated static results, in order to be able to return errors for issues due to resource exhaustion

### Ergonomics
I've tried to consider the ergeoomics of working with such explicitness, because boiler plate is inevitable when we want every cost or every decision to be laid bare in front of us. To that end, here are some initial ideas of how to make the idea workable 

- `?` to early return an error from a called function, e.g. `let a = i32.TryParse("gdgd")?;`
- A short hand default value to handle errors e.g. `let a = i32.TryParse("gdgd") or 0;`
- `defer` and `errordefer` to place fulfilment of the obligation alongside its creations
- `let` and `var` for (im)mutability
- Tagged unions with exhaustive pattern matching
- `Ok` token for the success case of `Result<void>`
- `Option<T>` with `if let Some(myValue) = someOption`
- Optional type inference
- Explicit ownership transfer with `give` and `take` keywords (no take-and-give-back)
- initially, `init` and `deinit` are magically known to be obligation-related. Eventual annotations could follow to make this explicit, and allow custom obligation behaviour
- Passing allocators to `deinit`, with compile time tracking that you used the correct allocator (As a guidance, you could either store a reference on the instance, or "just" pass in the correct one... I'm sure that's completely trivial!)
- destructuring with `take` semantics in order to fulfil obligations on the destructured object, but to take ownership of its constituent parts, e.g. `let (x, y, z) = take myPoint3d;`
- `deinit` that compile time enforces that you `deinit` all owned instances
- `0..5` and `0..=5` for exclusive/inclusive upper bounds

### Bring your own Runtime
Another idea I had was the ability to bolt on runtime features, for example Reflection and dynamic dispatch. The "default" runtime would be a completely empty set of hooks that are immediately compiled away. Any hook implementations would be self-contained (with declared dependencies) in order to allow multiple hooks for any relevant events. These features would be imported like libraries, so that any runtime costs incurred are apparant in code. These ideas are a loooooong way into the future, if this projet ever gets off the ground. 

## Deferred
There's already more than enough to get overwhelmed with, so some wishes are immediately deferred in order to try to get _something_ off the ground. 

### Features
- Auto-coercion from `*T` to `&T` / `&var T`. Start with explicit `&ptr` and `&var ptr` syntax everywhere, and refine later.
- c interop in order to maximise adoption. That way I get zig and c ffi.
- Any runtime extensions could be customized. E.g. imagine someone implementing an "Owning" GC allocator with runtime support, to manage your memory for you. This isn't a planned feature, but I like the idea: opt in the pay the cost of GC, and get GC.
- Extra obligations beyond allocation and result types, e.g. closing a DB connection or releasing a lock
- Implementation of `Tracked<T>` that adds a safe way to track the allocator used to allocate at the cost of a fat pointer. Considering if this would coerce to a &T in all cases.
- Adding threads and async. 
- Allowing shadowing if and only if initial obligation fulfilled
- take-and-give-back
- Access modifiers

### Things I don't know anything about
While I know very little about this stuff, I know nothing about implemntation of threads and async. Outside of the ability to move obligations into these things, I have no idea of the rammifications here.

## Challenges

- My knowledge. I know nothing, and have a lot to learn. I don't even know how to get off the ground yet.
- My time. I don't have much of it, and this is a massive project.
- The language will be pretty verbose, so strong ergonomics will be criticall
- The complexity of the obligation tracker
- Minimizing risk of use-after-free without lifetime annotations.
- The envisioned compilation times, due to the obligation tracker. (Would we want to enable skipping this part for "I've already validated this build once" scenarios?)
- Forward thinking reserved keywords - I'll likely need extra keywords for situations I've never thought about. How can I be as flexible as possible?s
- Many many many unknnown unknowns.
- Keeping the initial goal as absolutely small and simple as possible to get a working prototype.

# Syntax Reference

## Ownership and Borrowing

```
Pointers and References:
    *T       = owning pointer (obligation to free)
    &T       = immutable borrow (no obligation, no lifetime tracking - unsafe territory)
    &var T   = mutable borrow

Ownership Transfer:
    give       // caller gives ownership to callee
    take       // receiver takes ownership

    // Function signature: callee takes
    fn consume(take value: *T): void { ... }

    // Call site: caller gives
    consume(give myValue);

    // Assignment: receiver takes
    let a = take b;

    // Pattern matching: pattern takes from source
    if let Some(x) = take optionalOwned { ... }

    // Deinit signature: callee takes
    fn deinit(take self: *Self)

    // Call site, can omit passing giving the struct to its own destructor
    myInstance.deinit(&myAllocator);
```

## Example: DoublyLinkedList

```
pub struct DoublyLinkedList<T> {
    next: Option<*DoublyLinkedList<T>>,              // Owned pointer to next node
    unsafe previous: Option<&DoublyLinkedList<T>>,   // Unsafe borrow (use-after-free risk)
    value: Option<*T>,                               // Owned pointer to value

    pub fn init(allocator: &OwningAllocator, take value: Option<*T>): Result<*Self> {
        return allocator.create(Self {
            value: value,
            next: None,
            previous: None,
        });
    }

    pub fn append(self: &var Self, allocator: &OwningAllocator, take value: Option<*T>): Result<void> {
        if let Some(nextNode) = &var self.next {             // nextNode: &var *DoublyLinkedList<T>
            (&var *nextNode).append(allocator, give value)?; // deref to *, then &var borrow
        } else {
            var next = DoublyLinkedList<T>.init(allocator, give value)?;
            unsafe { (&var next).previous = Some(self); }
            self.next = Some(next);
        }
        return Ok;
    }

    pub fn deinit(take self: *Self, allocator: &OwningAllocator): void {
        if let Some(nextNode) = take self.next {  // explicit take ownership from self.next
            nextNode.deinit(allocator);
        }
        if let Some(val) = take self.value {      // explicit take ownership from self.value
            allocator.free(val);
        }
        allocator.free(self);
    }
}

pub fn exampleFunction(allocator: &OwningAllocator): Result<*DoublyLinkedList<Apple>> {
    var myList = DoublyLinkedList<Apple>.init(
        allocator,
        give Some(Apple.init(allocator)?)  // give ownership to init
    )?;

    (&var myList).append(
        allocator,
        give Some(Apple.init(allocator)?)
    )?;

    return Ok(myList);
}

pub fn main(): Result<i32> {
    var allocator = GeneralPurposeAllocator.init()?;
    defer allocator.deinit();

    var myList = exampleFunction(&allocator) or (error) {
        return Ok(1);
    };
    defer (&myList).deinit(&allocator);  // Note that deinit, as a function that takes the instance itself, can omit `give`ing the instance

    // Iteration: explicit borrows, no auto-coercion
    var activeNode: Option<&DoublyLinkedList<Apple>> = Some(&myList);  // &ptr borrows from *T
    while let Some(node) = activeNode {
        // process node...
        if let Some(nextPtr) = &node.next {   // borrow into Option, nextPtr: &*DoublyLinkedList<T>
            activeNode = Some(& *nextPtr);    // deref to *, then & borrow
        } else {
            activeNode = None;
        }
    }

    return Ok(0);
}
```

# Summary
A massive project, with my ideas of what a safe systems programmaming language could look like. Let's see if we ever get time to do anything about it.

Any obvious blunders, please educate me!

# Living notes to keep track of the decisions being made


Todo:
- Doubly linked list example

# AST decisions

### Statements and Expressions
- A block can `yield` a value to a binding `let foo = {yield 5;}`. This is an expression block.
- A block without yield is a statement
- `return` always exits the function
- `while` is always a statement
- `for` is always a statement
- `match` isnt yet designed

### Sugar and lowering
- `let x = somethingThatCanFail()?` lowes to `let x = somethingThatCanFail() onerror error { return error; }` for error propogation
- `let x = somethingThatCanFail() onerror 5`; lowers to `let x = somethingThatCanFail onerror { yield 5; }` to always bind even to an immutable to x.
- We probably need an onerror block to always `yield` or `return`
- Need some design to match success and error e.g. `if x is Error` kind of constructor
- Do we want type inference? Probably, but not certainly, at least if it's too hard to implement.
- `defer` and `errordefer` lowering not yet designed
- Ternary `let x = condition ? trueValue : falseValue;` lowers to `let x = if condition { yield trueValue; } else { yield falseValue; }`
- Probably lower `for` to a `while` statement

### Allocators
- We need some kind of root `SystemAllocator` - this is a special case given we need _something_ to serve as a root backing allocator

### Other
- Need to decide between `void` and `Unit` as the nothing type.
- Need to design the constructions for `Ok` `Some` `None` `Error` pattern matching
- We need a SystemAllocator that is a special exception that we must document. Users can write their own allicators and pull this in.
- `_` as discard, does not represent a bound value. All values must be bound or discarded. Discarded values cannot satisfy obligations.

```
if let Some(_) = take maybeValue {...} else {...}
```

### Error and Option bindings

We use pattern matching for Errors and options.

```
match take resultValue {
   var Ok(myValue) => {...}
   let Error(e) => {...}
}
```

The `if let Some(x) = y` from rust can be adopted here almost verbatim, our version looking like `if let Some(myBinding) = take maybeValue {...} else {...}`, and lowered to
```
match take resultValue {
   let Some(myValue) => {...}
   None => {...}
}
```

`if var Some(x) = y` would also be possible.

## Parser

### Strategy
- Pratt for expressions
- Recursive descernt for statements and declarations
- Every nud handles prefix position
- Every led handles infix/postfix position

### Operator Prescedencce
```
1  onerror
2  ||
3  &&
4  == !=
5  < > <= >=
6  + -
7  * /
8  unary: ! -
9  take
10 postfix: . () [] ?
```

### Associativity

Left associative: `+ - * / && || == != < > <= >=`
Right associative: `= onerror`


### Other
- Collect as many errors as possible in one compilation attempt, synchronizing on `;` and `}`
- `take` only valid only on expressions that produce an owned value
- Assignment is always a statement, never an expression
- How we deal with free functions in a namespace, and methods on a struct collisions (while generating our global symbol table)























































# Deferred


## Ownership Types

Byron has conceptually ownership types. Intially only `Owned<T>` will be implemented. Future types `Handle<T>`, `Unsafe<T>` and `Untracked<T>` as noted here are design stretch goals.

```
Owned<T>                       // Full ownership: Obligation authority with memory obligations.


// Not in scope for a very very very long time.

Handle<T>             // Delegated authority: Obligation authority without memory obligations.
Unsafe<T>             // memory-untracked ownership — memory provenance unverifiable, but obligations still tracked and enforced by compiler, for systems programming cases where fat pointer provenance tracking is genuinely unworkable.
                      // Every read and mutate requires the unsafe keyword.
                       
Untracked<T>          // Total suspension of the obligation tracker, reverting to Zig or C levels of trust. 
                      // Best-effort obligation tracking may be considered if we ever get here.
                      // Every read and mutate requires the untracked keyword
```


### Owned\<T\> — The Fat Pointer

`Owned<T>` is a fat pointer:

```
Owned<T> {
    ptr:       *T,
    allocator: *TransferringAllocator,
}
```

### Handle\<T\> — Obligation Authority

`Handle<T>` is a thin pointer wrapper:

```
Handle<T> {
    ptr: *T,
}
```

`Handle<T>` carries obligation authority over the instance but does not own the memory. Memory is owned by the `RetainingAllocator` that created it and freed when that allocator is deinited.

- Can call `@obligates`-decorated functions and burden the instance with new obligations
- Can call resolver functions to resolve obligations
- Has no way to free the underlying instance.




## Features
- Auto-coercion from `*T` to `&T` / `&var T`. Start with explicit `&ptr` and `&var ptr` syntax everywhere, and refine later.
- c interop in order to maximise adoption. That way I get zig and c ffi.
- Any runtime extensions could be customized. E.g. imagine someone implementing an "Owning" GC allocator with runtime support, to manage your memory for you. This isn't a planned feature, but I like the idea: opt in the pay the cost of GC, and get GC.
- Extra obligations beyond allocation and result types, e.g. closing a DB connection or releasing a lock
- Implementation of `Tracked<T>` that adds a safe way to track the allocator used to allocate at the cost of a fat pointer. Considering if this would coerce to a &T in all cases.
- Adding threads and async. 
- Allowing shadowing if and only if initial obligation fulfilled
- Access modifiers



# Some assorted notes to pick through - old thoughts and LLM additions to actually consider.


######################################################







// To decide: Do free and deinit expect to return `Result<void>` or `void`
// On the one hand, deinit is only supposed to deinit owned resources, and then free. On the other, it isn't guaranteed infallible. Edge cases (e.g. flushing an internal buffer as part of teardown, OS-level resource release that can fail) have not been fully examined. Revisit before stabilising the std.

### How Obligations Are Resolved

- Calling a defined resolver function (`commit`, `rollback`, `free`, etc.)
- Returning the bound instance, transferring the bound obligations to the caller
- `give`ing the bound instance to a function that is willing to accept obligation authority (denoted by a `take T` argument), transferring bound obligations to the receiver.

// todo: Assess this LLM addition for truth
**Resolvers must consume `self`** — either `take Self` for non-memory obligations, or `Owned<Self>` when memory must also be freed. Consuming the instance is what makes exactly-once resolution a structural guarantee — the binding no longer exists after the call, so a second resolution is impossible without separate tracking.

**The obligation tracker verifies at compile time that a listed resolver was called once, and only once, on every path.**

### The Result\<T, E\> obligation

`Result<T, E>` is an obligation with two resolvers — the `Ok` branch and the `Error` branch. Both must be handled on every code path.

---

`Owned<T>` carries a reference to the allocator that created it. This means:

- `self.free()` uses the embedded allocator — freeing from the wrong allocator isn't possible
- The compiler can verify that no `Owned<T>` outlives its allocator

`Owned<T>` provides a built-in `free()` method implemented in the standard library. This is what allows `@obligates([.free])` on `alloc` to work — the obligation is declared on the function, and the resolver is provided by the type. No user implementation required.

// TODO: Dispatch — `Owned<T>.free()` is a std built-in with the following shape:
//   fn free(self: take Owned<T>): Result<void> {
//       return self.allocator.free(give self)   // dispatch to allocator, return its Result directly
//   }
// `fn free<T>` on `TransferringAllocator` is the implementation-level target — each allocator's
// own business (return to pool, slab coalesce, no-op for arena, etc).
// Both are private for now. `fn free<T>` on the allocator interface becomes public when `Unsafe<T>`
// is in scope — that is the point where a user doing their own memory tracking needs direct
// allocator access without going through the fat pointer.
// Open: reading `self.allocator` before `give self` is a partial read from a consumed value —
// std-internal, likely requires unsafe at the implementation level.

// TODO: Allocator lifetime enforcement — the fat pointer gives us provenance: the obligation checker should prevent the allocator's own obligations from being resolved while any `Owned<T>` allocated from it still has a live `.free` obligation. This ordering enforces that `Owned<T>` is freed before the allocator is torn down, covering most use-after-free and double-free without a full lifetime analysis. Mechanism not yet designed.

The fat pointer is a deliberate early compromise in exchange for simpler obligation tracking. It _may_ be possible to eliminate it once the obligation checker is proven.


# The Obligation tracker (incomplete)

// Todo: This whole section needs close verification
## Binding States

Every `Owned<T>` and `Handle<T>` binding has a compile time state and an obligation set tracked through the control flow graph. The obligation set holds one live obligation of each declared kind — adding an obligation that is already live is a compile error.

```
Live              // safe to use, obligation unresolved
Discharged        // resolver called — binding is dead
Moved             // transferred to another owner — binding is dead
MaybeDischarged   // CFG join point with inconsistent discharge state
MaybeMoved        // CFG join point with inconsistent move state
```

Any state other than `Live` on access is a compile error:

```
var a = take List.init(&gpa)?;
a.deinit();
a.append(1)?;                        // COMPILE ERROR: "deinit" obligation on a is resolved

var b = take List.init(&gpa)?;
consume(give b)?;
b.append(1)?;                        // COMPILE ERROR: b is Moved

var c = take List.init(&gpa)?;
if (someCondition) {
    c.deinit();                      // c: Discharged on this branch only
}
c.append(1)?;                        // COMPILE ERROR: "deinit" obligation on c might be resolved
```

Assigning to a field of type `Owned<T>` when that field is `Live` is a compile error. The existing obligation must be resolved before the field can receive a new value. Example syntax depends on partial moves — deferred.

`&T` and `&var T` fields have no such restrictions since they carry no obligations.

---

## Ownership Transfer - give and take

`give` is a call-site keyword. `take` is a receiver keyword — used in parameter declarations as to denote that the function is designed to take ownership, and at call sites when accepting obligation authority from a return value or move.

```
give    // caller surrenders ownership into a function argument
take    // caller accepts the ownership of a returned instance from a call or a move
```

### Give

```
fn example(allocator: &TransferringAllocator): Result<void> {
    let myValue = take allocator.alloc(Node)?;
    consume(give myValue); 
}

fn consume(myValue: take Owned<MyType>): Result<void> { 
    consume(give myValue)?;
}

fn example(myValue: &var MyType): Result<void> {
    consume(give myValue)?;                     // COMPILE ERROR: No obligation authority
}
```

### Take











You are required to `take` ownership when a function expects to return an `Owned<T>`
```
var node = take Node.init(&gpa)?;    // correct
var node = Node.init(&gpa)?;         // COMPILE ERROR: Owned<T> must be taken
```

Moves must be `take`n
```
var node = take Node.init(&gpa)?;    
var a = take b;                      // correct. a is now live with b's obligations, b is inaccessible
var anotherNode = node;             // COMPILE ERROR: Move must be taken
```

Deconstruction must be `take`n
```
let (x, y, z) = take myPoint;       // correct
let (x, y, z) = myPoint;            // COMPILE ERROR: Move must be taken
```
---

## Unsafe Fields

// todo: is this needed now? Or much later. It would be needed in order to give a completely satisfactory `Owned<DoublyLinkedList>`. Might _not_ be needed in order to give a complete `Handle<DoublyLinkedList>`.

Fields carrying use-after-free or pointer safety risk are marked `unsafe` at declaration:

```
struct Node<T> {
    value:           T,
    next:            Option<Owned<Node<T>>>,
    unsafe previous: Option<&Node<T>>,   // back reference with a risk of use-after-free
}
```

**Setting** an unsafe field requires no annotation. 

```
node.previous = Some(&parent);
```
todo: due to the While this should always give a result, this isn't described on the field. Either Result wrapping is implicit due to the `unsafe` keyword, or should be described on the field.

**Reading or mutating** through an unsafe field requires `unsafe` at the access site and always produces a `Result`:

```
let prev = unsafe node.previous?;        // Result<Option<&Node<T>>, UnsafeError>
unsafe node.previous.doSomething()?;     // Result<void, UnsafeError>
```

---


## Results and Error Propagation

































# Deferred sections
## Handle<T> definition and examples

`Handle<T>` is a borrow that also transfers obligation authority. Think of it like a priviledged reference. 

You cannot burden or resolve obligations on an instance upon which you do not hold obligation authority. If you hold obligation authority, you must ensure resolution of all obligations in your set. See [Ownership Types](#ownership-types) for more information on ownership and obligation authority.

### Declaring obligations

// todo: This part needs to come later once `Handle<T>` is something we can thing about. 
// todo: Pull `Handle<T>` out to its own section when available. Don't delete the text, keep it, but it needs to all be in a deferred section.

_This section includes examples that are intended for future implementation_

The `@obligates` annotation declares that calling this function creates a live obligation on a binding. The obligation is associated with with the instance and must be resolved exactly once on every subsequent path.

### How Obligations Are Resolved

- Calling a defined resolver function (`myInstance.commit`, `myInstance.rollback`, `resolverFunction`, etc.)



- Returning the bound instance, transferring the bound obligations to the caller
- `give`ing the bound instance to a function that is willing to accept obligation authority (denoted by a `take Owned<T>` argument), transferring bound obligations to the receiver.

Single resolver on an instance

```
struct  TransferringAllocator {
    @obligates([.free])
    fn alloc<T>(self &var TransferringAllocator): Result<Owned<T>> {...}
}

fn foo(allocator: &var TransferringAllocator) {
    const myBar = allocator.alloc<Bar>();

    // Note that the obligation is to call .free on the myBar instance
    myBar.free();
}
```

Multiple resolvers on instance

``` Array semantics are **OR** — any one resolver in the list satisfies the obligation. 
@obligates([.commit, .rollback])
fn beginTransaction(connection: &var Connection): Result<Owned<Transaction>> {...}

fn foo(connection: &var Connection) {
    var transaction = beginTransaction(connection);

    // Note that the obligation is to call one of .commit or .rollback on the myBar instance
    myBar.commit();
}
```

Non-instance function resolver

```
@obligates([finish])
fn start(bar: &Bar): void {...}

fn finish(): void {...}

fn foo(bar: &Bar) {
    start(bar);

    // Note that the obligation is to call the finish function
    finish();
}
```

Compilation failures:

```
struct  TransferringAllocator {
    @obligates([.free])
    fn alloc<T>(self &var TransferringAllocator): Result<Owned<T>> {...}
}

@obligates([.commit, .rollback])
fn beginTransaction(connection: &var Connection): Result<Owned<Transaction>> {...}

fn foo(
    allocator: &var TransferringAllocator,
    connection: &var ConnectionFoo) {

    const myBar1 = allocator.alloc<Bar>();
    // myBar1.free(); // Did not resolve obligation or return the instance

    const myBar2 = allocator.alloc<Bar>();
    myBar2.free();
    myBar2.free(); // Tried to resolve obligation a second time

    var transaction = beginTransaction(connection);

    if(transaction.wasSuccessful){
        transaction.commit();
    }
    transaction.rollback(); // Tried to resolve obligation a second time through the transaction.wasSuccessful branch. 
}
```













































---
# LLM Segments

## Review Checkpoint

### Pre-Marker Issues Found

These are in the already-vetted sections and need separate fixes:

1. ~~**Binding States examples** — `a.deinit()`, `c.deinit()`, `old.deinit()` still reference dropped T.deinit.~~ Resolved — `deinit` is the correct resolver for user types (init/deinit pathway). Examples are correct.
2. ~~**RetainingAllocator interface**~~ Resolved — `alloc` now returns `Result<Handle<T>>`.
3. **`TransferringAllocator.free()` interface** — signature is `fn free<T>(self: &var Self, value: Owned<T>)` but usage is `node.free()` (method on the fat pointer with no allocator parameter). These are two different calling conventions. Needs a decision: is `.free()` a method on `Owned<T>` that uses the embedded allocator internally, or a method on the allocator that takes the value? Currently both appear in the same spec.
4. **Handle<T> description** — "Can call `@obligates`-decorated functions and burden the instance with new obligations" conflates two things: (a) calling factory functions that return new obligation-bearing values — valid now; (b) adding obligations to the Handle itself via a method — deferred. Separate these.
5. **Unsafe fields TODO** — `"todo: due to the While this should always give a result"` is a broken sentence. Unresolved: is `Result` wrapping on unsafe field reads implicit from the `unsafe` keyword, or must it be declared on the field?

### Outstanding Design Questions

These affect the sections below and need decisions before they can be finalized:

1. ~~**`@obligates` — "any one" vs "all must be called"**~~ Resolved — array semantics are OR. If AND is required, declare a wrapper function that calls both and obligate that.

2. ~~**Allocator cleanup resolver name**~~ Resolved — allocators follow the same `init`→`@obligates([.deinit])`→`deinit` pathway as any user type. `gpa.deinit()` is correct.

3. **`free()` — top-level only, or recursive into owned fields?**
   If T has `Owned<T>` fields, does `free()` recurse into them? Current implied answer: no — field cleanup requires partial moves (deferred). Confirm this is intentional.

4. **Handle<T> authority scope**
   Can a Handle<T> call `@obligates`-decorated factory functions that produce new obligation-bearing return values? (e.g. `handle.beginTransaction()` returning `Owned<Transaction>`.) Or is Handle only for resolving obligations already on it?

5. **CFG join point resolution**
   `MaybeDischarged` / `MaybeMoved` states are listed but the rules for how the compiler creates and resolves them at join points are not specified. Note: loop body obligations are resolved — a loop body is a scope, standard scope-exit rules apply.

6. **`unsafe` field Result-wrapping — implicit or explicit?**
   Reads through an unsafe field always return a `Result`. Is this implicit from the `unsafe` keyword on the field, or must it be declared explicitly on the field definition?

---

No `panic`. No `unwrap`. No hidden exit. Physical impossibilities — stack overflow, CPU fault — are the only aborts. Everything else is a `Result`.

```
?                       // Return, propagating error to caller
or 0                    // default value on error
or (error) { ... }      // explicit error handling
```

```
var a = take i32.parse("123")?          // propagates error to caller
var b = i32.parse("abc") or 0           // default on error
var c = i32.parse("abc") or (err) {     // explicit handling
    log.write(err)
    return Ok(0)
}
```

`Ok` is a reserved token:

```
return Ok               // Result<void> success
return Ok(value)        // Result<T> success
```

---

## Ergonomics

I've tried to consider some useful ergonomics to make this language workable, hopefully mitigating some of the admitedly heavy burden placed on the developer. 

```
let / var               // immutable / mutable binding
?                       // propagate Result error upward
or                      // default value on Err
defer                   // call at end of scope
errordefer              // call at end of scope on error path only
let (x, y) = take point // Destructuring
if let Some(x) = opt    // Option destructuring
0..5 / 0..=5            // exclusive / inclusive range
```

### defer

// LLM: "first-pass compilation transform" — stated as a confirmed implementation approach. Verify.
`defer` is a first-pass compilation transform. It moves a call to end of scope, allowing you to place resource cleanup next to its creation.

```
var node = take Node.init(&gpa)?;
defer node.deinit();           // inserted at end of scope
```

```
var node = take Node.init(&gpa)?;
defer node.deinit();
consume(give node)?;         // COMPILE ERROR: node Moved, defer would double resolve
```

// TODO: Fallible deinit — `deinit` returns `Result<void>`. What happens if it fails inside `defer`?
// Most languages sidestep the question: Rust's `Drop::drop` returns `()`, C++ destructors cannot
// propagate exceptions, Zig's deinit conventionally does not fail.
// Options: (a) defer silently discards the Result — contradicts no-hidden-failures goal.
//          (b) defer requires explicit `or { }` — burden on every defer.
//          (c) a failed deinit is a hard abort — leaked resource is unrecoverable.
// Retry / idempotent deinit: not a standard pattern. A failed deinit typically means accepted leak.
// Decision needed before implementation.

### errordefer

`errordefer` places a call at scope exit only when the scope exits with an error. The mechanism for cleaning up partial allocations:

// LLM: `assemble` is a placeholder not defined in the spec.
```
fn init(allocator: &TransferringAllocator): Result<Owned<Self>> {
    var a = take allocator.alloc(Part)?;
    errordefer a.deinit();           // runs only if something below fails

    var b = take allocator.alloc(Part)?;
    errordefer b.deinit();           // runs only if something below fails

    return Ok(assemble(give a, give b))   // happy path — caller owns both
}
```

`errordefer` with a fallible cleanup requires explicit acknowledgement:

```
errordefer a.deinit() or { log.write("cleanup failed, leak accepted") }
errordefer a.deinit() or { }       // empty block — acknowledged, accepted
```

---

## Safety Guarantees — Honest Assessment

// LLM: This section is largely LLM-authored. Each claim below needs author verification.

### Hard Compile Time Guarantees

// LLM: "Fat pointer provenance means allocator reclamation before Owned<T> is also a compile time error" — is the mechanism for this actually designed, or aspirational?
**No use after free** — binding states (`Live/Discharged/Moved`) tracked through CFG. Any access on a non-`Live` binding is a compile error. Fat pointer provenance means allocator reclamation before `Owned<T>` is also a compile time error.

**No double free** — `Discharged` state is terminal. Calling a resolver on a `Discharged` binding is a compile error. `give`/`take` transfer is singular and tracked. Two call sites cannot both resolve the same obligation.

**No silent obligation drop on field overwrite** — assigning to a `Live` `Owned<T>` field is a compile error. The old value must be resolved first.

### Strong Structural Guarantees

// LLM: "Every RetainingAllocator must be deinited" — how is this enforced? Does the RetainingAllocator have an @obligates([.deinit]) on its init?
**Memory leaks** — every `Owned<T>` must be resolved on every path. Every `RetainingAllocator` must be deinited. `errordefer` covers partial allocation failure paths. The structural guarantee is strong.

**Dangling resources** — DB connections, locks, streams, channels all participate in the same obligation system once `@obligates` is declared on their creating functions.

### Known Honest Gaps

**Empty or blocks** — `errordefer a.deinit() or { }` satisfies the compiler. The memory may be leaked. The compiler guarantees you acknowledged the failure, not that you handled it meaningfully.

**FFI boundaries** — resources returned from C without `@obligates` are invisible to the obligation checker. Wrapping correctly is the programmer's responsibility.

**Allocator implementation correctness** — the allocator contract is structurally verified. A buggy allocator implementation can still leak.

### The Honest Summary

// LLM: "Stronger than Zig in all cases" and "less complexity than Rust" are comparative claims. Verify these are claims you want to make, given the admitted gaps and deferred features above.
**Stronger than Zig in all cases. Comparable to Rust on memory leaks with more explicit and honest escape hatches. Hard compile time guarantees on use after free, double free, and dangling resources — achieved with less complexity than Rust.**

---

## When Byron Is The Wrong Tool

// LLM: This section was written by LLM. The content is reasonable but the tone ("The cost of opting out is designed to be felt") is LLM-flavoured. Accept or rewrite in your own voice.

Byron occupies a deliberate position. It is not trying to solve every problem.

If you find yourself frequently needing `Unsafe<T>` or `Untracked<T>` (when those are eventually available), Byron's ownership model is probably the wrong shape for that problem space. For code where manual untracked memory management is the norm and safety guarantees are not the goal, Zig or C are honest choices.

Byron's escape hatches exist for the occasional necessary exception, not as a comfortable alternative path through the language. The cost of opting out is designed to be felt.

---

## Syntax Reference

### Types — In Scope

```
Owned<T>                        // owning fat pointer — memory + obligation tracked
Handle<T>                       // obligation authority, no memory ownership — satisfies take T
Reference<T>          / &T      // immutable non-owning — no obligation
MutableReference<T>   / &var T  // mutable non-owning — no obligation
```

### Types — Reserved, Not In Scope

```
Unsafe<T>     / unsafe T        // memory-deaf ownership — memory provenance unverifiable,
                                // obligations still tracked. Every read and mutate
                                // requires the unsafe keyword at the operation site.

Untracked<T>  / untracked T     // total escape — memory unverifiable AND obligation
                                // tracker suspended. Zig/C levels of trust.
                                // Best-effort obligation tracking may be added later.
```

### Ownership Transfer

```
give        // caller surrenders ownership — call site only
take        // receiver capability declaration in parameter position — accepts Handle<T> or Owned<T>
            // also used at call site to accept obligation authority from a return value or move

// take T in parameter position — capability declaration, monomorphised at call site
fn consume(node: take Node): Result<void> { ... }
// Owned<T> in parameter position — when memory ownership is explicitly required
fn consume(node: Owned<Node>): Result<void> { ... }
// These are two alternative parameter patterns, not overloads of the same function.

// Receiving — take required
var node = take Node.init(&gpa)?

// Passing — give required
consume(give myNode)?

// Method receiver — implicit when self is Owned<T>
node.deinit()
defer node.deinit()

// Return — no keyword needed
fn init(...): Result<Owned<Node>> {
    return Ok(take allocator.alloc(Node)?)
}

// Move between bindings
var a = take b
let (x, y, z) = take myPoint
```

### Obligations

```
// Allocator level — .free() built into Owned<T> via fat pointer
@obligates([.free])
fn alloc<T>(self: &var TransferringAllocator): Result<Owned<T>> { ... }

// User level — init/deinit pair
@obligates([.deinit])
fn init(allocator: &TransferringAllocator): Result<Owned<Self>> { ... }

// Multiple valid resolvers — any one satisfies
@obligates([.commit, .rollback])
fn beginTransaction(conn: &Connection): Result<Owned<Transaction>> { ... }
```

### Results

```
?                       // propagate error upward
or 0                    // default value on Err
or (err) { ... }        // explicit handling
return Ok               // Result<void> success
return Ok(value)        // Result<T> success
```

---

## Example: Doubly Linked List

// LLM: The preamble below is LLM-written. Verify it represents your intent for what the example demonstrates.
The doubly linked list is the canonical hard case for manual memory — two pointers per node, back references, careful insert and cleanup. It exercises every ownership pattern Byron cares about.

`next` is `Owned` — ownership flows forward through the list.
`previous` is an `unsafe` field — a non-owning back reference. Setting it is fine. Reading it requires `unsafe` at the access site and always returns a `Result`. Under stack discipline the head always outlives the tail, so the reference is valid as long as the list is live — but the compiler cannot prove this, hence `unsafe`.

> **Note:** Borrowing `&T` from `Owned<T>` during traversal depends on deref coercion rules not yet fully defined. Those sections are marked TBD.

```
struct Node<T> {
    value:           T,
    next:            Option<Owned<Node<T>>>,
    unsafe previous: Option<&Node<T>>,

    @obligates([.deinit])
    fn init(allocator: &TransferringAllocator, value: T): Result<Owned<Self>> {
        var node = take allocator.alloc(Self)?
        node.value    = value
        node.next     = None
        node.previous = None
        return Ok(node)
    }

    fn append(self: &var Self, allocator: &TransferringAllocator, value: T): Result<void> {
        if let Some(next) = &var self.next {    // LLM: borrowing through Option<Owned<T>> — deref coercion TBD
            next.append(allocator, value)?
        } else {
            var next = take Node<T>.init(allocator, value)?
            next.previous = Some(self)      // SETTING unsafe field — no unsafe keyword needed
            self.next = Some(give next)
        }
        return Ok
    }

    fn deinit(self: Owned<Self>): Result<void> {
        // TODO: recursive cleanup of child nodes requires partial moves (deferred)
        self.free()
        return Ok
    }
}

fn exampleFunction(allocator: &TransferringAllocator): Result<Owned<Node<i32>>> {
    var list = take Node<i32>.init(allocator, 1)?
    (&var list).append(allocator, 2)?   // LLM: (&var list) explicit borrow syntax — not yet defined in spec
    (&var list).append(allocator, 3)?
    return Ok(list)
}

fn main(): Result<i32> {
    var gpa = take GeneralPurposeAllocator.init()?
    defer gpa.deinit()

    var list = take exampleFunction(&gpa) or (err) {
        return Ok(1)
    }
    defer list.deinit()     // TODO: recursive child cleanup deferred (partial moves)

    // Forward traversal — TBD (deref coercion rules not yet defined)
    var current: Option<&Node<i32>> = Some(&list)   // TBD
    while let Some(node) = current {
        // print node.value
        // current = borrow of node.next — TBD
    }

    // Backward traversal — unsafe only at the READ site
    var back: Option<&Node<i32>> = Some(tail)        // reaching tail: TBD
    while let Some(node) = back {
        // print node.value
        let prev = unsafe node.previous?    // Result<Option<&Node<i32>>, UnsafeError>
        back = prev
    }

    return Ok(0)

    // To verify obligation enforcement:
    // comment out: defer list.deinit()
    // COMPILE ERROR: obligation on 'list' not resolved on all paths
}
```

---

## Open Questions

- `@obligates` on interface methods — does the interface declare it, the implementation, or both?
- ~~Loop body obligations~~ Resolved — a loop body is a scope. Obligations created inside must be resolved, moved, or returned before scope end, same as any other scope. No special case.
- `@obligates([.free, .close])` — are all listed resolvers required, or any one? Currently `@obligates([.commit, .rollback])` reads as "any one satisfies" but a compound case like `.free` and `.close` implies both. Same syntax, different semantics?
- Deref coercion rules — borrowing `&T` from `Owned<T>` for traversal. No auto-coercion in current design. Explicit syntax TBD.
- `Owned<T>.free()` — built-in method using the fat pointer. Exact semantics relative to the allocator interface need formal definition. Is it a method on `Owned<T>` using the embedded allocator, or a method on the allocator taking the value?
- Partial moves — moving individual fields out of a struct before calling `self.free()`.
- `unsafe` field Result-wrapping — implicit from the `unsafe` keyword, or declared explicitly on the field?

---

## Deferred

Explicitly out of scope for the initial POC. Design space is reserved.

- Sigil shorthand for `Owned<T>` — deferred until POC usage patterns surface the right choice
- `Unsafe<T>` and `Untracked<T>` — reserved, documented above, not in scope
- `take`-and-give-back — temporarily borrowing ownership with a return obligation
- Shadowing — permitted only after initial obligation is fulfilled
- Access modifiers
- Traits and dynamic dispatch
- Threads and async
- C FFI interop
- Auto-coercion and deref rules
- Runtime extensions — bring your own GC, reflection, dynamic dispatch as explicit opt-in
- Extra obligation kinds beyond memory and Result — mechanism is general, stdlib declarations deferred
- Partial moves
- Burdening via method — `@obligates` on a method that takes and returns `Self` to accumulate obligations on an existing binding. Return type is `Result<T>`; `give`/`take` not needed in the return signature. Monomorphisation: `Owned<T>` in → `Owned<T>` out, `Handle<T>` in → `Handle<T>` out. TODO: design take-and-give-back syntax and CFG obligation-set semantics.
- Keyed, typed, or counted obligations — multiple live instances of the same obligation kind on one binding, delegated burden/resolve authority, sub-obligation scoping. Current model is one obligation of each kind per binding.

---

## Implementation Notes

- **Implementation language:** C#
- **Compiler target:** LLVM IR
- **POC target:** Doubly linked list with three levels, forward and backward traversal, obligation discharge verification — including compile failure on commented-out discharge
- **Obligation checker:** A CFG pass over the compiler's HIR, before lowering to LLVM IR. LLVM never sees obligations — they are erased at lowering.
// LLM: "A second annotation on the same CFG pass. Same infrastructure as the obligation checker." — implementation approach stated as decided. Verify.
- **Binding state tracker:** A second annotation on the same CFG pass. Same infrastructure as the obligation checker.

---

## Design Probe Notes

Questions that probe whether the design is internally consistent. None of these change the language design — they confirm it survives. Userspace never sees any of this.

### Root Allocator — What allocates the first allocator?

`Owned<T>` is a fat pointer containing `*TransferringAllocator`. Every `Owned<T>` must point to the allocator that created it. But if `GeneralPurposeAllocator.init()` returns `Owned<GPA>`, something had to allocate it — so what allocates the first allocator?

**Resolution:** `SystemAllocator` is a std-provided static backed by OS primitives (`mmap` / `VirtualAlloc`). It is not user-constructed and not `Owned<T>`. Its backing is the process. `GeneralPurposeAllocator.init()` takes no allocator argument — it uses `SystemAllocator` internally, setting it as the fat pointer's allocator. Userspace never sees or provides it.

```
// std only — not user-constructible
static SystemAllocator: TransferringAllocator   // backed by OS primitives

// user code — no backing allocator argument
var gpa = take GeneralPurposeAllocator.init()?
// gpa is Owned<GPA>. Fat pointer's allocator = SystemAllocator (internal detail).
// gpa.deinit() → gpa.free() → SystemAllocator.free() → munmap / VirtualFree
```

If a GPA backed by a different allocator were ever needed, that is simply a second factory function — same obligation pathway, no special cases:

```
@obligates([.deinit])
fn initWithBacking(allocator: &TransferringAllocator): Result<Owned<Self>> { ... }
```

All user allocators are `Owned<T>`, bottoming out at `SystemAllocator`. The chain is consistent. `SystemAllocator` is the one root that lives outside Byron's ownership model — owned by the process, not the language.

---

## Name

**Byron.** Short, not an acronym, not trying to be clever about what it does.

---

## Summary

A massive project. The core bet is that one primitive — obligations — can cover memory safety, error handling, and resource management uniformly, without a full borrow checker and without sacrificing explicit control.

The two axioms:

> **You cannot burden or resolve obligations on an instance upon which you do not hold obligation authority — `Owned<T>` or `Handle<T>`.**
> **If you hold obligation authority, you must ensure resolution of all obligations in your set.**

Everything else is the mechanism that proves them.




############################



Old readme




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

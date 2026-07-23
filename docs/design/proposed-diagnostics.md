# Proposed diagnostic designs

These designs are not implemented rules. Each requires representative Unity integration fixtures and false-positive tests before it can enter the diagnostic catalog.

## Hot-path object search calls

Detect semantically resolved object-search APIs inside known per-frame Unity callbacks. Risks include intentionally infrequent branches and user-configurable update-like names. Guards should require a Unity declaring symbol, a configurable hot-path method, and an invocation not protected by a provably one-time condition. Prefer diagnostic-only behavior; caching lifetime and invalidation are application-specific.

## LINQ or managed allocations in Unity update methods

Detect known allocating LINQ operators or explicit managed allocations in semantically identified update callbacks. Iterator composition, runtime-specific allocation behavior, and acceptable infrequent paths create false-positive risk. Guards should use resolved `System.Linq.Enumerable` symbols, configurable hot-path names, and exclude expression trees and editor-only code. A blanket rewrite is not safe.

## Strongly typed coroutine invocation

Suggest `StartCoroutine(Method())` for a constant string invocation only when overload resolution and a unique parameterless iterator method are provable. String invocations can deliberately use reflection or delayed argument binding, so the transformation is review-required and must not support Fix All.

## Repeated `Renderer.material` access

Repeated access can instantiate material copies, but replacing it with `sharedMaterial` changes asset mutation semantics and caching a material changes lifetime. A rule should initially diagnose repeated semantically resolved access only, document ownership implications, and avoid an automatic fix.

## Event subscription symmetry

Whole-type control-flow and Unity lifecycle behavior make symmetry difficult to prove. A narrow rule could pair direct `+=` statements in `OnEnable` with matching `-=` statements in `OnDisable`, using symbol equality for event, receiver, and handler. Conditional subscriptions, anonymous delegates, inheritance, and external ownership must be excluded. Start diagnostic-only.

## ECS structural changes inside query iteration

Structural changes can invalidate iteration or force synchronization. Guards should resolve both the active Entities query construct and structural-change API symbol, account for command buffers, and distinguish supported immediate main-thread cases. Because Entities versions differ, the rule must be version-symbol guarded and diagnostic-only until real-package tests establish conservative behavior.

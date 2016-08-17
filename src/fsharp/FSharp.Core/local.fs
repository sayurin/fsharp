// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.


namespace Microsoft.FSharp.Core

module DetailedExceptions =
    
    /// takes an argument, a formatting string, a param array to splice into the formatting string
    let inline internal invalidArgFmt (arg:string) (format:string) paramArray =    
        let msg = System.String.Format (format,paramArray)
        raise (new System.ArgumentException (msg,arg))

    /// takes a formatting string and a param array to splice into the formatting string
    let inline internal invalidOpFmt (format:string) paramArray =
        let msg = System.String.Format (format,paramArray)
        raise (new System.InvalidOperationException(msg))




namespace Microsoft.FSharp.Primitives.Basics 

open Microsoft.FSharp.Core
open Microsoft.FSharp.Core.DetailedExceptions
open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators
open Microsoft.FSharp.Collections
open Microsoft.FSharp.Core.Operators
open System.Diagnostics.CodeAnalysis                                    
open System.Collections.Generic
#if FX_NO_ICLONEABLE
open Microsoft.FSharp.Core.ICloneableExtensions            
#else
#endif  


module internal List = 

    let arrayZeroCreate (n:int) = (# "newarr !0" type ('T) n : 'T array #)

    [<SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")>]      
    let nonempty x = match x with [] -> false | _ -> true

    let rec iter f x = match x with [] -> () | h::t -> f h; iter f t

    // optimized mutation-based implementation. This code is only valid in fslib, where mutation of private
    // tail cons cells is permitted in carefully written library code.
    let inline setFreshConsTail cons t = cons.(::).1 <- t
    let inline freshConsNoTail h = h :: (# "ldnull" : 'T list #)

    let rec distinctToFreshConsTail cons (hashSet:HashSet<_>) list = 
        match list with
        | [] -> setFreshConsTail cons []
        | x::rest ->
            if hashSet.Add x then
                let cons2 = freshConsNoTail x
                setFreshConsTail cons cons2
                distinctToFreshConsTail cons2 hashSet rest
            else
                distinctToFreshConsTail cons hashSet rest

    let distinctWithComparer (comparer: IEqualityComparer<'T>) (list:'T list) =
        match list with
        | [] -> []
        | [h] -> [h]
        | x::rest ->
            let hashSet = HashSet<'T>(comparer)
            hashSet.Add x |> ignore
            let cons = freshConsNoTail x
            distinctToFreshConsTail cons hashSet rest
            cons

    let rec distinctByToFreshConsTail cons (hashSet:HashSet<_>) keyf list = 
        match list with
        | [] -> setFreshConsTail cons []
        | x::rest ->
            if hashSet.Add(keyf x) then
                let cons2 = freshConsNoTail x
                setFreshConsTail cons cons2
                distinctByToFreshConsTail cons2 hashSet keyf rest
            else
                distinctByToFreshConsTail cons hashSet keyf rest

    let distinctByWithComparer (comparer: IEqualityComparer<'Key>) (keyf:'T -> 'Key) (list:'T list) =       
        match list with
        | [] -> []
        | [h] -> [h]
        | x::rest ->
            let hashSet = HashSet<'Key>(comparer)
            hashSet.Add(keyf x) |> ignore
            let cons = freshConsNoTail x
            distinctByToFreshConsTail cons hashSet keyf rest
            cons
    
    let countBy (dict:Dictionary<_, int>) (keyf:'T -> 'Key) = 
        // No need to dispose enumerator Dispose does nothing.
        let mutable ie = dict.GetEnumerator()
        if not (ie.MoveNext()) then []
        else
            let res = freshConsNoTail (keyf ie.Current.Key, ie.Current.Value)
            let mutable cons = res
            while ie.MoveNext() do
                let cons2 = freshConsNoTail (keyf ie.Current.Key, ie.Current.Value)
                setFreshConsTail cons cons2
                cons <- cons2
            setFreshConsTail cons []
            res
    
    let rec pairwiseToFreshConsTail cons list lastvalue = 
        match list with
        | [] -> setFreshConsTail cons []
        | [h] -> setFreshConsTail cons [(lastvalue, h)]
        | h::t ->
            let cons2 = freshConsNoTail (lastvalue, h)
            setFreshConsTail cons cons2
            pairwiseToFreshConsTail cons2 t h

    let pairwise list =       
        match list with
        | [] -> []
        | [_] -> []
        | x1::x2::t ->
            let cons = freshConsNoTail (x1, x2)
            pairwiseToFreshConsTail cons t x2
            cons

    let rec chooseToFreshConsTail cons f xs =
        match xs with 
        | [] -> setFreshConsTail cons []
        | h::t -> 
            match f h with 
            | None -> chooseToFreshConsTail cons f t 
            | Some x -> 
                let cons2 = freshConsNoTail x
                setFreshConsTail cons cons2
                chooseToFreshConsTail cons2 f t

    let rec choose f xs =
        match xs with 
        | [] -> []
        | h::t -> 
            match f h with
            | None -> choose f t
            | Some x -> 
                let cons = freshConsNoTail x
                chooseToFreshConsTail cons f t
                cons
            

    let rec mapToFreshConsTail cons f x = 
        match x with
        | [] -> 
            setFreshConsTail cons []
        | h::t -> 
            let cons2 = freshConsNoTail (f h)
            setFreshConsTail cons cons2
            mapToFreshConsTail cons2 f t

    let map f x = 
        match x with
        | [] -> []
        | [h] -> [f h]
        | h::t -> 
            let cons = freshConsNoTail (f h)
            mapToFreshConsTail cons f t
            cons

    let rec mapiToFreshConsTail cons (f:OptimizedClosures.FSharpFunc<_,_,_>) x i = 
        match x with
        | [] -> 
            setFreshConsTail cons []
        | h::t -> 
            let cons2 = freshConsNoTail (f.Invoke(i,h))
            setFreshConsTail cons cons2
            mapiToFreshConsTail cons2 f t (i+1)

    let mapi f x = 
        match x with
        | [] -> []
        | [h] -> [f 0 h]
        | h::t -> 
            let f = OptimizedClosures.FSharpFunc<_,_,_>.Adapt(f)
            let cons = freshConsNoTail (f.Invoke(0,h))
            mapiToFreshConsTail cons f t 1
            cons

    let rec map2ToFreshConsTail cons (f:OptimizedClosures.FSharpFunc<_,_,_>) xs1 xs2 = 
        match xs1,xs2 with
        | [],[] -> 
            setFreshConsTail cons []
        | h1::t1, h2::t2 -> 
            let cons2 = freshConsNoTail (f.Invoke(h1,h2))
            setFreshConsTail cons cons2
            map2ToFreshConsTail cons2 f t1 t2
        | [],xs2 -> 
            invalidArgFmt "list1" 
                "{0}\nlist1 is {1} elements shorter than list2" 
                [|SR.GetString SR.listsHadDifferentLengths; xs2.Length|]
        | xs1,[] -> 
            invalidArgFmt "list2" 
                "{0}\nlist2 is {1} elements shorter than list1" 
                [|SR.GetString SR.listsHadDifferentLengths; xs1.Length|]

    let map2 f xs1 xs2 = 
        match xs1,xs2 with
        | [],[] -> []
        | h1::t1, h2::t2 -> 
            let f = OptimizedClosures.FSharpFunc<_,_,_>.Adapt(f)
            let cons = freshConsNoTail (f.Invoke(h1,h2))
            map2ToFreshConsTail cons f t1 t2
            cons
        | [],xs2 -> 
            invalidArgFmt "list1" 
                "{0}\nlist1 is {1} elements shorter than list2" 
                [|SR.GetString SR.listsHadDifferentLengths; xs2.Length|]
        | xs1,[] -> 
            invalidArgFmt "list2" 
                "{0}\nlist2 is {1} elements shorter than list1" 
                [|SR.GetString SR.listsHadDifferentLengths; xs1.Length|]

    let rec map3ToFreshConsTail cons (f:OptimizedClosures.FSharpFunc<_,_,_,_>) xs1 xs2 xs3 = 
        match xs1,xs2,xs3 with
        | [],[],[] ->
            setFreshConsTail cons []
        | h1::t1, h2::t2, h3::t3 -> 
            let cons2 = freshConsNoTail (f.Invoke(h1,h2,h3))
            setFreshConsTail cons cons2
            map3ToFreshConsTail cons2 f t1 t2 t3
        | xs1,xs2,xs3 -> 
            invalidArgFmt "list1, list2, list3" 
                "{0}\n list1.Length = {1}, list2.Length = {2}, list3.Length = {3}" 
                [|SR.GetString SR.listsHadDifferentLengths; xs1.Length; xs2.Length; xs3.Length|]

    let map3 f xs1 xs2 xs3 = 
        match xs1,xs2,xs3 with
        | [],[],[] -> []
        | h1::t1, h2::t2, h3::t3 -> 
            let f = OptimizedClosures.FSharpFunc<_,_,_,_>.Adapt(f)
            let cons = freshConsNoTail (f.Invoke(h1,h2,h3))
            map3ToFreshConsTail cons f t1 t2 t3
            cons
        | xs1,xs2,xs3 -> 
            invalidArgFmt "list1, list2, list3" 
                "{0}\n list1.Length = {1}, list2.Length = {2}, list3.Length = {3}" 
                [|SR.GetString SR.listsHadDifferentLengths; xs1.Length; xs2.Length; xs3.Length|]

    let rec mapi2ToFreshConsTail n cons (f:OptimizedClosures.FSharpFunc<_,_,_,_>) xs1 xs2 = 
        match xs1,xs2 with
        | [],[] -> 
            setFreshConsTail cons []
        | h1::t1, h2::t2 -> 
            let cons2 = freshConsNoTail (f.Invoke(n,h1,h2))
            setFreshConsTail cons cons2
            mapi2ToFreshConsTail (n + 1) cons2 f t1 t2
        | [],xs2 -> 
            invalidArgFmt "list1" 
                "{0}\nlist1 is {1} elements shorter than list2" 
                [|SR.GetString SR.listsHadDifferentLengths; xs2.Length|]
        | xs1,[] -> 
            invalidArgFmt "list2" 
                "{0}\nlist2 is {1} elements shorter than list1" 
                [|SR.GetString SR.listsHadDifferentLengths; xs1.Length|]

    let mapi2 f xs1 xs2 = 
        match xs1,xs2 with
        | [],[] -> []
        | h1::t1, h2::t2 -> 
            let f = OptimizedClosures.FSharpFunc<_,_,_,_>.Adapt(f)
            let cons = freshConsNoTail (f.Invoke(0, h1,h2))
            mapi2ToFreshConsTail 1 cons f t1 t2
            cons
        | [],xs2 -> 
            invalidArgFmt "list1" 
                "{0}\nlist1 is {1} elements shorter than list2" 
                [|SR.GetString SR.listsHadDifferentLengths; xs2.Length|]
        | xs1,[] -> 
            invalidArgFmt "list2" 
                "{0}\nlist2 is {1} elements shorter than list1" 
                [|SR.GetString SR.listsHadDifferentLengths; xs1.Length|]
    
    let rec scanToFreshConsTail cons xs s (f: OptimizedClosures.FSharpFunc<_,_,_>) =
        match xs with
        | [] ->
            setFreshConsTail cons []
        | h::t ->
            let newState = f.Invoke(s,h)
            let cons2 = freshConsNoTail newState
            setFreshConsTail cons cons2
            scanToFreshConsTail cons2 t newState f

    let scan f (s:'State) (list:'T list) = 
        let f = OptimizedClosures.FSharpFunc<_,_,_>.Adapt(f)
        match list with 
        | [] -> [s]
        | _ -> 
            let cons = freshConsNoTail s
            scanToFreshConsTail cons list s f
            cons

    let rec indexedToFreshConsTail cons xs i =
        match xs with
        | [] ->
            setFreshConsTail cons []
        | h::t ->
            let cons2 = freshConsNoTail (i,h)
            setFreshConsTail cons cons2
            indexedToFreshConsTail cons2 t (i+1)

    let indexed xs =
        match xs with
        | [] -> []
        | [h] -> [(0,h)]
        | h::t ->
            let cons = freshConsNoTail (0,h)
            indexedToFreshConsTail cons t 1
            cons

    let rec mapFoldToFreshConsTail cons (f:OptimizedClosures.FSharpFunc<'State, 'T, 'U * 'State>) acc xs =
        match xs with
        | [] ->
            setFreshConsTail cons []
            acc
        | h::t ->
            let x',s' = f.Invoke(acc,h)
            let cons2 = freshConsNoTail x'
            setFreshConsTail cons cons2
            mapFoldToFreshConsTail cons2 f s' t

    let mapFold f acc xs =
        match xs with
        | [] -> [], acc
        | [h] ->
            let x',s' = f acc h
            [x'],s'
        | h::t ->
            let f = OptimizedClosures.FSharpFunc<_,_,_>.Adapt(f)
            let x',s' = f.Invoke(acc,h)
            let cons = freshConsNoTail x'
            let s' = mapFoldToFreshConsTail cons f s' t
            cons, s'

    let rec forall f xs1 = 
        match xs1 with 
        | [] -> true
        | h1::t1 -> f h1 && forall f t1

    let rec exists f xs1 = 
        match xs1 with 
        | [] -> false
        | h1::t1 -> f h1 || exists f t1

    // optimized mutation-based implementation. This code is only valid in fslib, where mutation of private
    // tail cons cells is permitted in carefully written library code.
    let rec revAcc xs acc = 
        match xs with 
        | [] -> acc
        | h::t -> revAcc t (h::acc)

    let rev xs = 
        match xs with 
        | [] -> xs
        | [_] -> xs
        | h1::h2::t -> revAcc t [h2;h1]

    // return the last cons it the chain
    let rec appendToFreshConsTail cons xs = 
        match xs with 
        | [] -> 
            setFreshConsTail cons xs // note, xs = []
            cons
        | h::t -> 
            let cons2 = freshConsNoTail h
            setFreshConsTail cons cons2
            appendToFreshConsTail cons2 t

    // optimized mutation-based implementation. This code is only valid in fslib, where mutation of private
    // tail cons cells is permitted in carefully written library code.
    let rec collectToFreshConsTail (f:'T -> 'U list) (list:'T list) cons = 
        match list with 
        | [] -> 
            setFreshConsTail cons []
        | h::t -> 
            collectToFreshConsTail f t (appendToFreshConsTail cons (f h))

    let rec collect (f:'T -> 'U list) (list:'T list) = 
        match list with
        | [] -> []
        | [h] -> f h
        | _ ->
            let cons = freshConsNoTail (Unchecked.defaultof<'U>)
            collectToFreshConsTail f list cons
            cons.Tail 

    let rec allPairsToFreshConsTailSingle x ys cons =
        match ys with
        | [] -> cons
        | h2::t2 ->
            let cons2 = freshConsNoTail (x,h2)
            setFreshConsTail cons cons2
            allPairsToFreshConsTailSingle x t2 cons2

    let rec allPairsToFreshConsTail xs ys cons =
        match xs with
        | [] -> setFreshConsTail cons []
        | h::t ->
            let p = allPairsToFreshConsTailSingle h ys cons
            allPairsToFreshConsTail t ys p

    let allPairs (xs:'T list) (ys:'U list) =
        match xs, ys with
        | _, [] -> []
        | [], _ -> []
        | _ ->
            let cons = freshConsNoTail (Unchecked.defaultof<'T * 'U>)
            allPairsToFreshConsTail xs ys cons
            cons.Tail

    // optimized mutation-based implementation. This code is only valid in fslib, where mutation of private
    // tail cons cells is permitted in carefully written library code.
    let rec filterToFreshConsTail cons f l = 
        match l with 
        | [] -> 
            setFreshConsTail cons l // note, l = nil
        | h::t -> 
            if f h then 
                let cons2 = freshConsNoTail h 
                setFreshConsTail cons cons2
                filterToFreshConsTail cons2 f t
            else 
                filterToFreshConsTail cons f t
      
    let rec filter f l = 
        match l with 
        | [] -> l
        | h :: ([] as nil) -> if f h then l else nil
        | h::t -> 
            if f h then   
                let cons = freshConsNoTail h 
                filterToFreshConsTail cons f t
                cons
            else 
                filter f t

    let iteri f x = 
        let f = OptimizedClosures.FSharpFunc<_,_,_>.Adapt(f)
        let rec loop n x = 
            match x with 
            | [] -> () 
            | h::t -> f.Invoke(n,h); loop (n+1) t

        loop 0 x

    // optimized mutation-based implementation. This code is only valid in fslib, where mutation of private
    // tail cons cells is permitted in carefully written library code.
    let rec concatToFreshConsTail cons h1 l = 
        match l with 
        | [] -> setFreshConsTail cons h1
        | h2::t -> concatToFreshConsTail (appendToFreshConsTail cons h1) h2 t
      
    // optimized mutation-based implementation. This code is only valid in fslib, where mutation of private
    // tail cons cells is permitted in carefully written library code.
    let rec concatToEmpty l = 
        match l with 
        | [] -> []
        | []::t -> concatToEmpty t 
        | (h::t1)::tt2 -> 
            let res = freshConsNoTail h
            concatToFreshConsTail res t1 tt2
            res

    let toArray (l:'T list) =
        let len = l.Length
        let res = arrayZeroCreate len
        let rec loop i l =
            match l with
            | [] -> ()
            | h::t ->
                res.[i] <- h
                loop (i+1) t
        loop 0 l
        res

    let ofArray (arr:'T[]) =        
        let mutable res = ([]: 'T list)
        for i = arr.Length-1 downto 0 do
            res <- arr.[i] :: res
        res

    let inline ofSeq (e : IEnumerable<'T>) =
        match e with
        | :? list<'T> as l -> l
        | :? ('T[]) as arr -> ofArray arr
        | _ ->
            use ie = e.GetEnumerator()
            if not (ie.MoveNext()) then []
            else
                let res = freshConsNoTail ie.Current
                let mutable cons = res
                while ie.MoveNext() do
                    let cons2 = freshConsNoTail ie.Current
                    setFreshConsTail cons cons2
                    cons <- cons2
                setFreshConsTail cons []
                res

    let concat (l : seq<_>) = 
        match ofSeq l with
        | [] -> []
        | [h] -> h
        | [h1;h2] -> h1 @ h2
        | l -> concatToEmpty l

    let rec initToFreshConsTail cons i n f = 
        if i < n then 
            let cons2 = freshConsNoTail (f i)
            setFreshConsTail cons cons2
            initToFreshConsTail cons2 (i+1) n f 
        else 
            setFreshConsTail cons []
           
      
    let init count f = 
        if count < 0 then 
            invalidArgFmt "count" "{0}\ncount = {1}" [|LanguagePrimitives.ErrorStrings.InputMustBeNonNegativeString; count|]
        if count = 0 then [] 
        else 
            let res = freshConsNoTail (f 0)
            initToFreshConsTail res 1 count f
            res

    let rec takeFreshConsTail cons n l =
        if n = 0 then setFreshConsTail cons [] else
        match l with
        | [] -> 
            invalidOpFmt 
                "{0}\nThe list was short by {1} {2}" 
                [|SR.GetString SR.notEnoughElements; n; (if n=1 then "element" else "elements")|]
        | x::xs ->
            let cons2 = freshConsNoTail x
            setFreshConsTail cons cons2
            takeFreshConsTail cons2 (n - 1) xs
 
    let take n l =
        if n < 0 then invalidArgFmt "count" "{0}\ncount = {1}" [|LanguagePrimitives.ErrorStrings.InputMustBeNonNegativeString; n|]
        if n = 0 then [] else 
        match l with
        | [] -> 
            invalidOpFmt 
                "{0}\nThe list was short by {1} {2}" 
                [|SR.GetString SR.notEnoughElements; n; (if n=1 then "element" else "elements")|]
        | x::xs ->
            let cons = freshConsNoTail x
            takeFreshConsTail cons (n - 1) xs
            cons

    let rec splitAtFreshConsTail cons index l =
        if index = 0 then
            setFreshConsTail cons []
            l
        else
        match l with
        | [] -> 
            invalidOpFmt 
                "{0}\nThe list was short by {1} {2}" 
                [|SR.GetString SR.notEnoughElements; index; (if index=1 then "element" else "elements")|]
        | x :: xs ->
                let cons2 = freshConsNoTail x
                setFreshConsTail cons cons2
                splitAtFreshConsTail cons2 (index - 1) xs
 
    let splitAt index l =
        if index < 0 then invalidArgFmt "index" "{0}\nindex = {1}" [|SR.GetString SR.inputMustBeNonNegative; index|]
        if index = 0 then [], l else
        match l with
        | []  -> invalidOp (SR.GetString SR.inputListWasEmpty)
        | [_] -> 
            if index = 1 then l, [] else 
            invalidOpFmt 
                "{0}\nThe list was {1} {2} shorter than the index" 
                [|SR.GetString SR.notEnoughElements; index-1; (if index=2 then "element" else "elements")|]
            
        | x::xs ->
            if index = 1 then [x], xs else
            let cons = freshConsNoTail x
            let tail = splitAtFreshConsTail cons (index - 1) xs
            cons, tail    
      
    // optimized mutation-based implementation. This code is only valid in fslib, where mutation of private
    // tail cons cells is permitted in carefully written library code.
    let rec partitionToFreshConsTails consL consR p l = 
        match l with 
        | [] -> 
            setFreshConsTail consL l // note, l = nil
            setFreshConsTail consR l // note, l = nil
            
        | h::t -> 
            let cons' = freshConsNoTail h
            if p h then 
                setFreshConsTail consL cons'
                partitionToFreshConsTails cons' consR p t
            else 
                setFreshConsTail consR cons'
                partitionToFreshConsTails consL cons' p t
      
    let rec partitionToFreshConsTailLeft consL p l = 
        match l with 
        | [] -> 
            setFreshConsTail consL l // note, l = nil
            l // note, l = nil
        | h::t -> 
            let cons' = freshConsNoTail h 
            if p h then 
                setFreshConsTail consL cons'
                partitionToFreshConsTailLeft cons'  p t
            else 
                partitionToFreshConsTails consL cons' p t
                cons'

    let rec partitionToFreshConsTailRight consR p l = 
        match l with 
        | [] -> 
            setFreshConsTail consR l // note, l = nil
            l // note, l = nil
        | h::t -> 
            let cons' = freshConsNoTail h 
            if p h then 
                partitionToFreshConsTails cons' consR p t
                cons'
            else 
                setFreshConsTail consR cons'
                partitionToFreshConsTailRight cons' p t

    let partition p l = 
        match l with 
        | [] -> l,l
        | h :: ([] as nil) -> if p h then l,nil else nil,l
        | h::t -> 
            let cons = freshConsNoTail h 
            if p h 
            then cons, (partitionToFreshConsTailLeft cons p t)
            else (partitionToFreshConsTailRight cons p t), cons

    let rec truncateToFreshConsTail cons count list =
        if count = 0 then setFreshConsTail cons [] else
        match list with
        | [] -> setFreshConsTail cons []
        | h::t ->
            let cons2 = freshConsNoTail h
            setFreshConsTail cons cons2
            truncateToFreshConsTail cons2 (count-1) t

    let truncate count list =
        match list with
        | [] -> list
        | _ :: ([] as nil) -> if count > 0 then list else nil
        | h::t ->
            if count <= 0 then []
            else
                let cons = freshConsNoTail h
                truncateToFreshConsTail cons (count-1) t
                cons

    let rec unfoldToFreshConsTail cons f s =
        match f s with
        | None -> setFreshConsTail cons []
        | Some (x,s') ->
            let cons2 = freshConsNoTail x
            setFreshConsTail cons cons2
            unfoldToFreshConsTail cons2 f s'

    let unfold (f:'State -> ('T * 'State) option) (s:'State) =
        match f s with
        | None -> []
        | Some (x,s') ->
            let cons = freshConsNoTail x
            unfoldToFreshConsTail cons f s'
            cons

    // optimized mutation-based implementation. This code is only valid in fslib, where mutation of private
    // tail cons cells is permitted in carefully written library code.
    let rec unzipToFreshConsTail cons1a cons1b x = 
        match x with 
        | [] -> 
            setFreshConsTail cons1a []
            setFreshConsTail cons1b []
        | (h1,h2)::t -> 
            let cons2a = freshConsNoTail h1
            let cons2b = freshConsNoTail h2
            setFreshConsTail cons1a cons2a
            setFreshConsTail cons1b cons2b
            unzipToFreshConsTail cons2a cons2b t

    // optimized mutation-based implementation. This code is only valid in fslib, where mutation of private
    // tail cons cells is permitted in carefully written library code.
    let unzip x = 
        match x with 
        | [] -> 
            [],[]
        | (h1,h2)::t -> 
            let res1a = freshConsNoTail h1
            let res1b = freshConsNoTail h2
            unzipToFreshConsTail res1a res1b t
            res1a,res1b

    // optimized mutation-based implementation. This code is only valid in fslib, where mutation of private
    // tail cons cells is permitted in carefully written library code.
    let rec unzip3ToFreshConsTail cons1a cons1b cons1c x = 
        match x with 
        | [] -> 
            setFreshConsTail cons1a []
            setFreshConsTail cons1b []
            setFreshConsTail cons1c []
        | (h1,h2,h3)::t -> 
            let cons2a = freshConsNoTail h1
            let cons2b = freshConsNoTail h2
            let cons2c = freshConsNoTail h3
            setFreshConsTail cons1a cons2a
            setFreshConsTail cons1b cons2b
            setFreshConsTail cons1c cons2c
            unzip3ToFreshConsTail cons2a cons2b cons2c t

    // optimized mutation-based implementation. This code is only valid in fslib, where mutation of private
    // tail cons cells is permitted in carefully written library code.
    let unzip3 x = 
        match x with 
        | [] -> 
            [],[],[]
        | (h1,h2,h3)::t -> 
            let res1a = freshConsNoTail h1
            let res1b = freshConsNoTail h2
            let res1c = freshConsNoTail h3 
            unzip3ToFreshConsTail res1a res1b res1c t
            res1a,res1b,res1c

    let rec windowedToFreshConsTail cons windowSize i list =
        if i = 0 then
            setFreshConsTail cons []
        else
            let cons2 = freshConsNoTail <| take windowSize list
            setFreshConsTail cons cons2
            windowedToFreshConsTail cons2 windowSize (i - 1) list.Tail

    let windowed windowSize (list: 'T list) =
        if windowSize <= 0 then invalidArgFmt "windowSize" "{0}\nwindowSize = {1}" [|SR.GetString SR.inputMustBePositive; windowSize|]
        let len = list.Length
        if windowSize > len then
            []
        else
            let cons = freshConsNoTail <| take windowSize list
            windowedToFreshConsTail cons windowSize (len - windowSize) list.Tail
            cons

    let rec chunkBySizeToFreshConsTail chunkCons resCons chunkSize i list =
        match list with
        | [] ->
            setFreshConsTail chunkCons []
            setFreshConsTail resCons []
        | h::t ->
            let cons = freshConsNoTail h
            if i = chunkSize then
                setFreshConsTail chunkCons []
                let newResCons = freshConsNoTail cons
                setFreshConsTail resCons newResCons
                chunkBySizeToFreshConsTail cons newResCons chunkSize 1 t
            else
                setFreshConsTail chunkCons cons
                chunkBySizeToFreshConsTail cons resCons chunkSize (i+1) t

    let chunkBySize chunkSize list =
        if chunkSize <= 0 then invalidArgFmt "chunkSize" "{0}\nwindowSize = {1}" [|SR.GetString SR.inputMustBePositive; chunkSize|]
        match list with
        | [] -> []
        | head::tail ->
            let chunkCons = freshConsNoTail head
            let res = freshConsNoTail chunkCons
            chunkBySizeToFreshConsTail chunkCons res chunkSize 1 tail
            res

    let rec splitIntoToFreshConsTail chunkCons resCons lenDivCount lenModCount i j list =
        match list with
        | [] ->
            setFreshConsTail chunkCons []
            setFreshConsTail resCons []
        | h::t ->
            let cons = freshConsNoTail h
            if (i < lenModCount && j = lenDivCount + 1) || (i >= lenModCount && j = lenDivCount) then
                setFreshConsTail chunkCons []
                let newResCons = freshConsNoTail cons
                setFreshConsTail resCons newResCons
                splitIntoToFreshConsTail cons newResCons lenDivCount lenModCount (i + 1) 1 t
            else
                setFreshConsTail chunkCons cons
                splitIntoToFreshConsTail cons resCons lenDivCount lenModCount i (j + 1) t

    let splitInto count (list: _ list) =
        if count <= 0 then invalidArgFmt "count" "{0}\ncount = {1}" [|SR.GetString SR.inputMustBePositive; count|]
        match list.Length with
        | 0 -> []
        | len ->
            let chunkCons = freshConsNoTail list.Head
            let res = freshConsNoTail chunkCons
            let count = min len count
            splitIntoToFreshConsTail chunkCons res (len / count) (len % count) 0 1 list.Tail
            res

    // optimized mutation-based implementation. This code is only valid in fslib, where mutation of private
    // tail cons cells is permitted in carefully written library code.
    let rec zipToFreshConsTail cons xs1 xs2 = 
        match xs1,xs2 with 
        | [],[] -> 
            setFreshConsTail cons []
        | h1::t1, h2::t2 -> 
            let cons2 = freshConsNoTail (h1,h2)
            setFreshConsTail cons cons2
            zipToFreshConsTail cons2 t1 t2
        | [],xs2 -> 
            invalidArgFmt "list1" 
                "{0}\nlist1 is {1} elements shorter than list2" 
                [|SR.GetString SR.listsHadDifferentLengths; xs2.Length|]
        | xs1,[] -> 
            invalidArgFmt "list2" 
                "{0}\nlist2 is {1} elements shorter than list1" 
                [|SR.GetString SR.listsHadDifferentLengths; xs1.Length|]

    // optimized mutation-based implementation. This code is only valid in fslib, where mutation of private
    // tail cons cells is permitted in carefully written library code.
    let zip xs1 xs2 = 
        match xs1,xs2 with 
        | [],[] -> []
        | h1::t1, h2::t2 -> 
            let res = freshConsNoTail (h1,h2)
            zipToFreshConsTail res t1 t2
            res
        | [],xs2 -> 
            invalidArgFmt "list1" 
                "{0}\nlist1 is {1} elements shorter than list2" 
                [|SR.GetString SR.listsHadDifferentLengths; xs2.Length|]
        | xs1,[] -> 
            invalidArgFmt "list2" 
                "{0}\nlist2 is {1} elements shorter than list1" 
                [|SR.GetString SR.listsHadDifferentLengths; xs1.Length|]

    // optimized mutation-based implementation. This code is only valid in fslib, where mutation of private
    // tail cons cells is permitted in carefully written library code.
    let rec zip3ToFreshConsTail cons xs1 xs2 xs3 = 
        match xs1,xs2,xs3 with 
        | [],[],[] -> 
            setFreshConsTail cons []
        | h1::t1, h2::t2, h3::t3 -> 
            let cons2 = freshConsNoTail (h1,h2,h3)
            setFreshConsTail cons cons2
            zip3ToFreshConsTail cons2 t1 t2 t3
        | xs1,xs2,xs3 -> 
            invalidArgFmt "list1, list2, list3" 
                "{0}\n list1.Length = {1}, list2.Length = {2}, list3.Length = {3}" 
                [|SR.GetString SR.listsHadDifferentLengths; xs1.Length; xs2.Length; xs3.Length|]

    // optimized mutation-based implementation. This code is only valid in fslib, where mutation of private
    // tail cons cells is permitted in carefully written library code.
    let zip3 xs1 xs2 xs3 = 
        match xs1,xs2,xs3 with 
        | [],[],[] -> 
            []
        | h1::t1, h2::t2, h3::t3 -> 
            let res = freshConsNoTail (h1,h2,h3) 
            zip3ToFreshConsTail res t1 t2 t3
            res
        | xs1,xs2,xs3 -> 
            invalidArgFmt "list1, list2, list3" 
                "{0}\n list1.Length = {1}, list2.Length = {2}, list3.Length = {3}" 
                [|SR.GetString SR.listsHadDifferentLengths; xs1.Length; xs2.Length; xs3.Length|]

    let rec takeWhileFreshConsTail cons p l =
        match l with
        | [] -> setFreshConsTail cons []
        | x::xs ->
            if p x then
                let cons2 = freshConsNoTail x
                setFreshConsTail cons cons2
                takeWhileFreshConsTail cons2 p xs
            else
                setFreshConsTail cons []

    let takeWhile p (l: 'T list) =
        match l with
        | [] -> l
        | x :: ([] as nil) -> if p x then l else nil
        | x::xs ->
            if not (p x) then [] else
            let cons = freshConsNoTail x
            takeWhileFreshConsTail cons p xs
            cons

module internal Array = 

    open System
    open System.Collections.Generic

#if FX_NO_ARRAY_KEY_SORT
    // Mimic behavior of BCL QSort routine, used under the hood by various array sorting APIs
    let qsort<'Key,'Value>(keys : 'Key[], values : 'Value[], start : int, last : int, comparer : IComparer<'Key>) =  
            let valuesExist = 
                match values with
                | null -> false
                | _ -> true
                
            let swap (p1, p2) =
                let tk = keys.[p1]
                keys.[p1] <- keys.[p2]
                keys.[p2] <- tk
                if valuesExist then
                    let tv = values.[p1]
                    values.[p1] <- values.[p2]
                    values.[p2] <- tv
                    
            let partition (left, right, pivot) =
                let value = keys.[pivot]
                swap (pivot, right)
                let mutable store = left
                
                for i in left..(right - 1) do
                    if comparer.Compare(keys.[i],value) < 0 then
                        swap(i, store)
                        store <- store + 1

                swap (store, right)
                store
            
            let rec qs (left, right) =
                if left < right then
                    let pivot = left + (right-left)/2
                    let newpivot = partition(left,right,pivot)
                    qs(left,newpivot - 1)
                    qs(newpivot+1,right)
            
            qs(start, last)
            
    type System.Array with
        static member Sort<'Key,'Value>(keys : 'Key[], values : 'Value[], comparer : IComparer<'Key>) =
            let valuesExist = 
                match values with
                | null -> false
                | _ -> true
            match keys,values with
            | null,_ -> raise (ArgumentNullException())
            | _,_ when valuesExist && (keys.Length <> values.Length) -> raise (ArgumentException())
            | _,_ -> qsort(keys, values, 0, keys.Length-1, comparer)

        static member Sort<'Key,'Value  when 'Key : comparison>(keys : 'Key[], values : 'Value[]) =
            let valuesExist = 
                match values with
                | null -> false
                | _ -> true
            match keys,values with
            | null,_ -> raise (ArgumentNullException())
            | _,_ when valuesExist && (keys.Length <> values.Length) -> raise (ArgumentException())
            | _,_ -> qsort(keys,values,0,keys.Length-1,LanguagePrimitives.FastGenericComparer<'Key>)

        static member Sort<'Key,'Value>(keys : 'Key[], values : 'Value[], start : int, length : int, comparer : IComparer<'Key>) =
            match keys with
            | null -> raise (ArgumentNullException())
            | _ -> qsort(keys,values,start,start+length-1,comparer)
#else
#endif

    let inline fastComparerForArraySort<'t when 't : comparison> () =
#if FX_NO_ARRAY_KEY_SORT
        LanguagePrimitives.FastGenericComparer<'t>
#else
        LanguagePrimitives.FastGenericComparerCanBeNull<'t>
#endif        

    // The input parameter should be checked by callers if necessary
    let inline zeroCreateUnchecked (count:int) = 
        (# "newarr !0" type ('T) count : 'T array #)

    let inline init (count:int) (f: int -> 'T) = 
        if count < 0 then invalidArgFmt "count" "{0}\ncount = {1}" [|LanguagePrimitives.ErrorStrings.InputMustBeNonNegativeString; count|]
        let arr = (zeroCreateUnchecked count : 'T array)  
        for i = 0 to arr.Length-1 do 
            arr.[i] <- f i
        arr

    let inline indexNotFound() = raise (KeyNotFoundException(SR.GetString(SR.keyNotFoundAlt)))

    let findBack f (array: _[]) =
        let rec loop i =
            if i < 0 then indexNotFound()
            elif f array.[i] then array.[i]
            else loop (i - 1)
        loop (array.Length - 1)

    let tryFindBack f (array: _[]) =
        let rec loop i =
            if i < 0 then None
            elif f array.[i] then Some array.[i]
            else loop (i - 1)
        loop (array.Length - 1)

    let findIndexBack f (array: _[]) =
        let rec loop i =
            if i < 0 then indexNotFound()
            elif f array.[i] then i
            else loop (i - 1)
        loop (array.Length - 1)

    let tryFindIndexBack f (array: _[]) =
        let rec loop i =
            if i < 0 then None
            elif f array.[i] then Some i
            else loop (i - 1)
        loop (array.Length - 1)

    let permute indexMap (arr : _[]) = 
        let res  = zeroCreateUnchecked arr.Length
        let inv = zeroCreateUnchecked arr.Length
        for i = 0 to arr.Length - 1 do 
            let j = indexMap i 
            if j < 0 || j >= arr.Length then invalidArg "indexMap" (SR.GetString(SR.notAPermutation))
            res.[j] <- arr.[i]
            inv.[j] <- 1uy
        for i = 0 to arr.Length - 1 do 
            if inv.[i] <> 1uy then invalidArg "indexMap" (SR.GetString(SR.notAPermutation))
        res

    let mapFold f acc (array : _[]) =
        match array.Length with
        | 0 -> [| |], acc
        | len ->
            let f = OptimizedClosures.FSharpFunc<_,_,_>.Adapt(f)
            let mutable acc = acc
            let res = zeroCreateUnchecked len
            for i = 0 to array.Length-1 do
                let h',s' = f.Invoke(acc,array.[i])
                res.[i] <- h'
                acc <- s'
            res, acc

    let mapFoldBack f (array : _[]) acc =
        match array.Length with
        | 0 -> [| |], acc
        | len ->
            let f = OptimizedClosures.FSharpFunc<_,_,_>.Adapt(f)
            let mutable acc = acc
            let res = zeroCreateUnchecked len
            for i = len - 1 downto 0 do
                let h',s' = f.Invoke(array.[i],acc)
                res.[i] <- h'
                acc <- s'
            res, acc

    let scanSubRight f (array : _[]) start fin initState =
        let f = OptimizedClosures.FSharpFunc<_,_,_>.Adapt(f)
        let mutable state = initState
        let res = zeroCreateUnchecked (fin-start+2)
        res.[fin - start + 1] <- state
        for i = fin downto start do
            state <- f.Invoke(array.[i], state)
            res.[i - start] <- state
        res

    let unstableSortInPlaceBy (f: 'T -> 'U) (array : array<'T>) =
        let len = array.Length 
        if len < 2 then () 
        else
            let keys = zeroCreateUnchecked array.Length
            for i = 0 to array.Length - 1 do 
                keys.[i] <- f array.[i]
            Array.Sort<_,_>(keys, array, fastComparerForArraySort())

    let unstableSortInPlace (array : array<'T>) = 
        let len = array.Length 
        if len < 2 then () 
        else Array.Sort<_>(array, fastComparerForArraySort())

    let stableSortWithKeysAndComparer (cFast:IComparer<'Key>) (c:IComparer<'Key>) (array:array<'T>) (keys:array<'Key>)  =
        // 'places' is an array or integers storing the permutation performed by the sort
        let places = zeroCreateUnchecked array.Length 
        for i = 0 to array.Length - 1 do 
            places.[i] <- i 
        System.Array.Sort<_,_>(keys, places, cFast)
        // 'array2' is a copy of the original values
        let array2 = (array.Clone() :?> array<'T>)

        // Walk through any chunks where the keys are equal
        let mutable i = 0
        let len = array.Length
        let intCompare = fastComparerForArraySort<int>()
            
        while i < len do 
            let mutable j = i
            let ki = keys.[i]
            while j < len && (j = i || c.Compare(ki, keys.[j]) = 0) do 
               j <- j + 1
            // Copy the values into the result array and re-sort the chunk if needed by the original place indexes
            for n = i to j - 1 do
               array.[n] <- array2.[places.[n]]
            if j - i >= 2 then
                Array.Sort<_,_>(places, array, i, j-i, intCompare)
            i <- j

    let stableSortWithKeys (array:array<'T>) (keys:array<'Key>) =
        let cFast = fastComparerForArraySort()
        let c = LanguagePrimitives.FastGenericComparer<'Key>
        stableSortWithKeysAndComparer cFast c array keys

    let stableSortInPlaceBy (f: 'T -> 'U) (array : array<'T>) =
        let len = array.Length 
        if len < 2 then () 
        else
            // 'keys' is an array storing the projected keys
            let keys = zeroCreateUnchecked array.Length
            for i = 0 to array.Length - 1 do 
                keys.[i] <- f array.[i]
            stableSortWithKeys array keys

    let stableSortInPlace (array : array<'T>) =
        let len = array.Length 
        if len < 2 then () 
        else
            let cFast = LanguagePrimitives.FastGenericComparerCanBeNull<'T>
            match cFast with 
            | null -> 
                // An optimization for the cases where the keys and values coincide and do not have identity, e.g. are integers
                // In this case an unstable sort is just as good as a stable sort (and faster)
                Array.Sort<_,_>(array, null)
            | _ -> 
                // 'keys' is an array storing the projected keys
                let keys = (array.Clone() :?> array<'T>)
                stableSortWithKeys array keys

    let stableSortInPlaceWith (comparer:'T -> 'T -> int) (array : array<'T>) =
        let len = array.Length
        if len > 1 then
            let keys = (array.Clone() :?> array<'T>)
            let comparer = OptimizedClosures.FSharpFunc<_,_,_>.Adapt(comparer)
            let c = { new IComparer<'T> with member __.Compare(x,y) = comparer.Invoke(x,y) }
            stableSortWithKeysAndComparer c c array keys

    let inline subUnchecked startIndex count (array : 'T[]) =
        let res = zeroCreateUnchecked count : 'T[]
        if count < 64 then
            for i = 0 to res.Length-1 do
                res.[i] <- array.[startIndex+i]
        else
            Array.Copy(array, startIndex, res, 0, count)
        res

    let splitInto count (array : 'T[]) =
        let len = array.Length
        if len = 0 then
            [| |]
        else
            let count = min count len
            let res = zeroCreateUnchecked count : 'T[][]
            let minChunkSize = len / count
            let startIndex = ref 0
            for i = 0 to len % count - 1 do
                res.[i] <- subUnchecked !startIndex (minChunkSize + 1) array
                startIndex := !startIndex + minChunkSize + 1
            for i = len % count to count - 1 do
                res.[i] <- subUnchecked !startIndex minChunkSize array
                startIndex := !startIndex + minChunkSize
            res

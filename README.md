usfxr
=====

usfxr is a C# library to easily generate (and play) sounds in real time inside Unity. It is used to synthesize audio for typical game-related actions such as item pickups, jumps, lasers, hits, explosions, and more.

It is a Unity-compatible C# port of Thomas Vian's [as3sfxr](https://code.google.com/p/as3sfxr/), which itself is an ActionScript 3 port of Tomas Pettersson's [sfxr](http://www.drpetter.se/project_sfxr.html).

Despite my name not being Thomas or a variant of it, I found myself wishing for a (free) library to procedurally generate audio inside Unity, and usfxr is the result of it.

I make no claims in regards to the source code or interface, since it was simply adapted from Thomas Vian's own code and elegant interface. As such, usfxr contains the same features offered by as3sfxr, including:

* Asynchronous caching
* Cache-during-first-play
* Automatic cache clearing on parameter change
* Faster synthesis

Additionally, it also allows:

* Asynchronous playback and generation (via Coroutines)

If you're just looking for a few good sound effects to use, anyone can use sound files generated by [as3sfxr's online version](http://www.superflashbros.net/as3sfxr/) without any changes. However, using usfxr directly allows:

* Since audio is generated in real time, there's no storage of audio files as assets necessary (making compiled project sizes smaller)
* Easy variations of every sound (like jumps) to add more flavor do the user experience

Of course, because sounds need to be synthesized prior to being played, this means there's an impact to performance when an audio play is triggered. The impact of this is mitigated as much as possible by the library (via caching and Coroutine execution), but can also be worked around by pre-caching audio prior to the start of a game. Check the samples for examples of this approach.








TODO:
* Test if SfxrParams.pow() is actually faster than using pow
* Test if SfxrParams.to4DP() is returning numbers correctly (1.00001 becomes 1.00000, etc) - maybe round it?
* Replace Random.value with a different function? The original used Math.random(), which returns 0 <= n < 1, while Random.value returns 0 <= n <= 1
* if float.parse(str) already returns 0 on empty strings, so SfxrParams.setSettingsString() can be simpler and faster
* replace getTimer() on SfxrSynth with a Time specific call?

* Line 496 of SfxrSynth: awkward conversion (was implying from float to int): _changeLimit = (int)((1f - p.changeSpeed) * (1f - p.changeSpeed) * 20000f + 32f);
* Line 682 of SfxrSynth: awkward conversion (was implying from float to int): _phase = _phase - (int)_periodTemp;

Missing aspects:
* onEnterFrame ticker
* events/callbacks


Features:
* Use Coroutines/yield to asynchronously create the data

Hi, welcome and thanks for your interest in AudioStream !


Please read carefully before anything else!
===========================================

AudioStream uses FMOD Studio functionality, which redistribution is not allowed for 3rd party SDKs ( such as this one ).
Therefore, when you first import AudioStream into a new project a bunch of compile errors CS0246 will occur ('The type or namespace name `FMOD' could not be found. Are you missing a using directive or an assembly reference?')
This is normal and will be resolved once you manually import the FMOD Studio Unity package.

The FMOD Studio Unity package is available at

== FMOD downloads: https://www.fmod.com/download

You will need "Unity FMOD Studio Unity integrations" from the page, Version at least 1.10.00 - all later versions _should_ work too.
Warning: Versions prior to 1.10.00 are no longer supported.

==== 
NOTE: you have to create an account and agree to the FMOD EULA before downloading and you are bound by it by using this asset (their licensing policy is very friendly to indies though).

==== 
NOTE: AudioStream uses only low level API of FMOD Studio and only really requires part of the "Plugins" folder from the FMOD package.
(the Plugins folder contains C# wrapper for FMOD and all necessary platform specific plugins, the rest of the package enables usage of FMOD Studio projects and objects directly in Unity, live editing of FMOD project and access to other FMOD Studio project capabilities.)

In general you need only native platforms plugins, and low level FMOD C# wrapper.
That means you need all native plugins in 'Plugins' folder, except 'Plugins/FMOD/*.cs' sources and 'Plugins/Editor' folder, but you need the whole 'Plugins/FMOD/Wrapper/' folder.

You are free to delete everything else, atm this means:
( FMOD specific files in ) Editor, Editor Default Resources, Gizmos, Resources, StreamingAssets, Plugins/Editor and all files in Plugins/FMOD _EXCEPT_ Plugins/FMOD/Wrapper.
( You might want to include Plugins/FMOD/LICENSE.TXT, and Plugins/FMOD/fmodplugins.cpp if you need/want acces to iOS plugins such as Resonance/GoogleVR plugins - these are generally not needed though)

You project structure just with AudioStream and needed FMOD parts should look like this afterwards:

-------------------                                   -------------------
| Assets          | AudioStream       Android         | Wrapper         | fmod_dsp.cs
------------------------------------------------------------------------- fmod_errors.cs
| ProjectSettings | Plugins         | FMOD            |                   fmod_studio.cs
                  -------------------------------------                   fmod.cs
                                      fmodstudio.bundle
                                      fmodstudioL.bundle
                                      gvraudio.bundle
                                      iOS
                                      Metro
                                      tvOS
                                      UWP
                                      x86
                                      x86_64
                                  
The above is for v 1.10 of the plugin

Once the FMOD Studio Unity package is successfully imported and setup, AudioStream is ready to use.

You can move AudioStream folder freely anywhere in the project, for example into Plugins to reduce user scripts compile times.


===========================================
Usage instructions:
===========================================

AudioStream provides two main ways to stream audio - the first completely bypasses Unity audio (playing at stream specified sample rate), but is capable of playing just the audio signal from the stream 1:1, while the second one
behaves like a standard AudioSource enabling all usual functionality such as Unity 3D spatialisation, effecting and mixing with other Unity audio sources.

First component is
  AudioStreamMinimal,
  component for 'normal' Unity AudioSource is called AudioStream.
  
Both have reliable network loss detection and recovery and can stream all supported audio formats locally or over the network.

NOTE: by default FMOD autodetects format of the stream automatically. This works reliable on desktops and Android, but iOS requires selecting correct audio format for each respective stream manually.
It is important to select correct audio type, though - if wrong format is selected ( for example mpeg for Ogg Vorbis radio/stream ), FMOD will probably not play it and might run unrecoverable problems when starting/Playing and Stopping/releasing the stream.

Each component further provides selective Console logging and UnityEvent messages.

As a source a http link to a PLS or M3U/8 playlist distributed commonly by web/net radio stations, a http link to a remote file such as podcast which is then streamed in full,
or a local filesystem path to a file can be specified.

====
NOTE: the first entry from playlist is played. This is usually the correct stream, but if there are more than one streams specified
you'd have to extract desired stream/link from the downloaded playlist manually.

====
NOTE: Any necessary resampling is done by Unity automatically.

====
NOTE: AudioStreamMinimal has better starvation recovery on less reliable networks, so if all Unity audio features are not needed I generally recommend using it for just for 1:1 playback of the stream audio.
You can influence how streaming behaves (how much does it take to wait) while attempting to connect and under starvation condition by modifying 'Initial Connection Retry Count' and 'Starving Retry Count' under Advanced settings.




Streaming was tested on iOS, Android, Windows and OS X.




===========================================
Supported formats:
===========================================

AudioStream can stream all formats FMOD recognises. For complete list see https://en.wikipedia.org/wiki/FMOD#File_formats
RAW format is supported also by exposing format values in the AudioStream/AudioStreamMinimal Inspector since these must be set explicitly by user.
For GVR specific formats see below.


===========================================
(Advanced/Custom) Speaker mode :
===========================================

When RAW speaker mode is selected for custom setups you should also provide No. of speakers, and consider providing custom mix matrix, if needed.
See http://www.fmod.org/documentation/#content/generated/FMOD_SPEAKERMODE.html and http://www.fmod.org/documentation/#content/generated/FMOD_Channel_SetMixMatrix.html for details.
Good place to call setMixMatrix on opened channel is in 'OnPlaybackStarted' event.


===========================================
Non system default audio output:
===========================================

As of 1.3 it is possible to specify other than system default audio output for AudioStreamMinimal component directly and for AudioStream ( or any AudioSource ) via
AudioSourceOutputDevice component.

AudioSourceOutputDevice can be used separately from AudioStream for any AudioSource but you have to start and stop the redirection manually (if not using its automatic startup).
For use with AudioStream keep automatic startup disabled as AudioStream detects AudioSourceOutputDevice automatically and starts redirection too after the stream is acquired.

The output is set by Output Driver ID - 0 means current system default output. For all driver IDs currently recognised by FMOD in your system please run the OutputDeviceDemo demo scene and see
its Start() method where all outputs are evaluated and printed to Console.

If you don't need output redirection it is better to not attach AudioSourceOutputDevice as it currently has some performance implications even when outputting to default device ( driver with id 0 ) - this is true _especially_ for mobiles.

Note: AudioSourceOutputDevice allows chaining of audio filters, the last one in the chain should have 'Mute After Routing' enabled. - it is therefore possible to output single AudioSource to multiple outputs at the same time.

Note: this component introduces some latency since it needs to effectively sample OAFR buffer before passing it on to FMOD. - 'Best latency' setting in Unity Audio settings is recommended.

Note: at least version 1.08.11 of FMOD Unity Integration is needed as it contains a bug fix for AudioSourceOutputDevice to work.

Please consider redirection a desktop class feature - due to variety of different issues I had on my testing Android and iOS devices (with various versions of Unity and relevant development tools) I don't recommend using it on mobiles in general, but
some Android phones seem to be dealing with it fine starting with 1.5.1, though. YMMV. I was unable to run it on iOS.


===========================================
Audio input:
===========================================

As of version 1.4 it is possible to stream audio from any available system recording input - just attach AudioSourceInput component to an empty game object and from custom script access audio buffer data of AudioSource which it automatically creates.
See how to interact with it in the AudioStreamInputDemo scene.
Latency is rather high for spatializable input streams since it has to go via full Unity audio buffer processing. For (significantly) lower latency you can use AudioSourceInput2D component since 1.5.2. - with downside that it is 2D only.

Currently this is best option I could come up with, for even lower latency native plugin is needed ( such as https://github.com/keijiro/Lasp ), with but the same 2D limitation since it uses just OnAudioFilterRead
[ OnAudioFilterRead has limitation of not being able to support 3D sound ]. For LASP interop with AudioSource see this gist: https://gist.github.com/r618/d74f07b6049fce20f1dc0dfa546bda89 ( LASP have to patched though currently since it can't be run not from main thread, and frequency is not exposed - those are just minor changes).
AudioSourceInput2D latency is very usable though and e.g. on iOS has very good response to audio input in the scene.

AudioSourceInput* components try to set very small FMOD DSP buffer at Start - this might, or might not be what you want - YMMV, update in source if needed for now.


For iOS/mobiles see specific recording notes below.


===========================================
3D spatialisation
===========================================

As of 1.5 all streams including input can be fully spatialised by Unity by adjusting Spatial Blend on AudioSource.
Possibly the most simple usage is demonstrated in the UnitySpatializerDemo scene together with AudioStream, and AudioStreamInput.
AudioStreamInput has 3D support, but higher latency - see above.

1.6 introduced support for GoogleVR spatializer plugin - see GVRSourceDemo, and GVRSoundfieldDemo scenes how to use it.
No special setup is needed - just provide source link/path on the component as usual, and modify exposed [3D] parameters.
You can set your own listener transform, or it defaults to main camera transform if not specified.

GVRSource accepts all formats as normal AudioStream/FMOD component.
GVRSoundfield can play only a-, and b-format ambisonic files.

GVR playback is currently in the same category as AudioStreamMinimal (i.e. no AudioSource support)

Since Google is now providing its own proper 3D audio Unity integration in the form of Resonance Audio I recommend using it (https://developers.google.com/resonance-audio/ , Unity 2017.1 and up)
AudioStream can be used just like any other AudioSource, so it's sufficient to just add AudioStream component to Resonance enabled game object and everything will just work.

GoogleVR provided by FMOD currently will be deprecated by FMOD 1.11, it includes separate resonance libraries, but due to above I recommend using Google's integration better.
AudioStream sill includes GVR demo scenes, which will be replaced by Resonance, or possibly completely removed in future versions.


===========================================
GOAudioSaveToFile:
===========================================

utility script allows automatic saving of audio being played on GO to file in StreamingAssets as WAV file.
You can drive it also externally by passing write data yourself - just uncheck useThisGameObjectAudio in that case.


===========================================
iOS Build notes:
===========================================

- Since FMOD Version 1.09 added support for Google VR and the respective plugin is not Bitcode enabled it is necessary to not build Xcode project with Bitcode support - or, if you don't need Google VR plugin, simply delete libgvraudio plugin/library.
- arbitrary loads for app transport security settings should be enabled ( newer versions of Unity handle this automatically via Allow HTTP downloads in Player settings ) in order to stream internet content via HTTP.


===========================================
iOS recording notes:
===========================================

- check 'Prepare iOS For Recording' in iOS Player Settings / Other settings
- ensure that DSP Bufer Size in Project Settings - Audio is set to 'Best latency' - otherwise recording won't work; - tested on Unity 5.3.5f1 - unsure if applicable for all later versions.
- add 'Privacy - Microphone Usage Description' ( raw value NSMicrophoneUsageDescription ) key and its value to Info.plist ( Target / Info ) in generated Xcode project.
Newer versions of Unity allow you to specify this key in iOS Player Settings via 'Microphone usage description' ( this is needed due to privacy concerns, iOS will ask for confirmation before first usage of the microphone ).
- when 'Prepare iOS For Recording' is selected - from the manual: 'When selected, the microphone recording APIs are initialised. This makes recording latency lower, though on iPhones it re-routes audio output via earphones only.' ( note: here 'earphones' should be more likely 'earspeaker' )
Since 1.4.1 AudioStream provides 'fix' for this situation with included iOS plugin which requests an audio route override for normal playback to be on speaker/headset; recording uses normal route, i.e. recorded output is on earspeaker/default.
Newer ( I think 2017.2 and up ) versions of Unity provide setting for this in iOS Player Settings - I have not yet tested this so far but it would require removing iOSSpeaker.h and iOSSpeaker.m from AudioStream/Plugins/iOS and removing it's usage from AudioStreamInputBase.cs


===========================================
Basic background audio mode on mobiles:
===========================================

iOS:
====
	On all relatively recent versions of iOS this should be sufficient to properly enable audio background mode:

	- In Unity, iOS Player Settings:

		- set 'Behavior in Background': Custom
		- enable 'Audio, AirPlay, PiP'

		this generates appropriate entries in Xcode project:
			- Application does not run in background    NO
			- Required background modes
					- Item 0                                App plays audio or streams audio/video using AirPlay

		(you can inspect them manually if needed)

	- modify UnityApplicationController.mm:

                #import <AVFoundation/AVAudioSession.h>

				In

                - (void)applicationWillResignActive:(UIApplication*)application
                OR
                - (void)applicationDidEnterBackground:(UIApplication*)application
                OR
                - (void)startUnity:(UIApplication*)application

				add:

				[[AVAudioSession sharedInstance] setCategory:AVAudioSessionCategoryPlayback error:nil];
				[[AVAudioSession sharedInstance] setActive: YES error: nil];


				- you can use other appropriate category if needed, such as  AVAudioSessionCategoryRecord, or AVAudioSessionCategoryPlayAndRecord

	- the application's audio should now not be interrupted when entering background.
	For full implementation including ability to start/stop playback by the user when in the backgound from Control center/lock screen working remote events are needed, but I had no luck enabling this with Unity audio so far.

=======
Android:

	On Android Gradle build system and Android Studio are needed:

	- In Unity, Build Settings:
		- select Build System: Gradle
		- Export Project checked

	- Android Studio:
		- import Project/Gradle build script
		- find and open UnityPlayerActivity.java in src/main/java folder
		- comment out onPause method

	As in the case of iOS above these are the basics - you won't get music controls avaiable for the user on the lock screen for example.


===========================================
General Unity / mobile / FMOD plugin notes:
===========================================

Currently (11/2017) any later than 1.10.00 version of FMOD Unity Integrations is recommended. ( 1.10.00 had a bug causing tag info not to be retrievable ).

Package is submitted with Unity version 5.5.4p5 since this is lowest version supporting APFS on macOS.

Unity 5.3.5 and later for mobile applications is recommended in general since previous versions had plethora of varying issues starting with building and ending with ~strike~dragons/~strike~ runtime crashes for unknown reasons.
Standalone should be fine for runtime from 5.0 except with various quality of life issues in the Editor.



===========================================
AudioStream and network audio:
===========================================

As of 1.7.1 AudioStream includes support for making any AudioSource to become an Icecast source via IcecastSource component.
IcecastSource processes OnAudioFilterRead signal, optionally encodes it, and pushes result to opened source connection to Icecast, which can be then connected to by any streaming client.
IcecastSource configuration should match Icecast source and mountpoint definition - all fields are hopefully comprehensibly annotated in the Editor with tooltips.
Icecast 2.4.0 + is supported.

Icecast settings are reasonably well documented directly in the xml configuration file; for common testing it is enough to set 
<hostname> (for server URL) and in <listen-socket> section <bind-address> (for IP the source to connect to) and optionally <shoutcast-mount>
- these should match IcecastSource fields and it should then automatically connect and start pushing whatever content is being played in OnAudioFilterRead.

In the IcecastSourceDemo scene is an example configuration for a site specific local Icecast server with an AudioStream radio being as source.


Currently OGGVORBIS can be chosen as audio codec for the signal (default), or raw PCM without any encoding.
PCM data is fast, but has rather significant disadvantage in occupying very high network bandwidth - between 100 - 200 kB/s depending on Unity audio settings.
For OGGVORBIS encoding a custom modified MIT licensed library is used, available here: https://github.com/r618/.NET-Ogg-Vorbis-Encoder .
Note: this is very likely not an ideal implementation and I'm not entirely satisfied with it - I made it run much faster than original, but it still barely fits into OnAudioFilterRead timeslot.
If you know of any other C# only Vorbis encoding library, please let me know!

For PCM server/source:
	- make sure the AudioStream client uses exactly the same audio properties, i.e. samplerate, channels, and byte format as originating machine with IcecastSource
	( this means not the format of the audio it is playing, but properties of its audio output as reported e.g. by AudioSettings.outputSampleRate, byte format should be PCM16 under most cirumstances),
	otherwise the signals won't match and connection will be dropped.

For OGGVORBIS server/source:
	- Default or Best Peformance DSP Buffer Size in Audio Settings is recommended to provide larger audio buffer
	- Currently only supports 40k+ Stereo VBR encoding
	- bitrate is currently determined automatically by Icecast; I haven't found a reliable way to explicitely set/influence this on Icecast source so far; on my setup it is anywhere between 300-500 kbps.
	- any common streaming client/webbrowser (including AudioStream) can play this source by connecting to the Icecast mountpoint/instance.
	- currently, AudioStream client seems to have better performance with this stream than AudioStreamMinimal. It is necessary to set rather high (~ 300k) Stream Block Alignment on AudioStreamMinimal to connect and stream from this source due to probably higher bitrate and its refresh rate not being ideal for it.

Encoding using OPUS is planned for direct - peer to peer - audio in AudioStream, and in Ogg container as OGGOPUS codec for Icecast source.
OPUS have better network and performance characteristics and better configurability - with downside that FMOD, unfortunately, does not support OPUS family of codecs.
OGGOPUS Icecast stream will still be playable by common streaming clients and browsers.


===========================================

In case of any questions / suggestion feel free to ask on support forum. Often things change without notice, especially like setting up and building to all various/mobile platforms.

Thanks!

Martin



===========================================

V 1.0 062016
- initial version.

V 1.1 082016
- update for FMOD Version 1.08.09 <- Strongly recommended to update, if previous version was used.
- update for Unity 5.4
- improved startup for variable buffer length streams
- improved stream loss detection and recovery
- improved stream finishing for finite streams ( hosted files ) and locally streamed files for AudioStream component
- improved state reporting
- few performance improvements - AudioStream is now even more low profile 

V 1.2 082016
- added option to select any available audio output in the system including non system default one for AudioStreamMinimal

V 1.3 092016
- new AudioSourceOutputDevice component - enables redirection of AudioSource's output buffer to any audio output present in the system
- update for FMOD Version 1.08.11 <- at least this version is needed for AudioSourceOutputDevice to work as it contains a bug fix formerly preventing so.
- fixed tags reporting on track change
- ( refactored common functionality into a new source file )

V 1.4 042017
- new AudioSourceInput component - allows recording audio from any available recording device.
- update for FMOD Version 1.09.03 ( it was not completely necessary, you can stay on any previous version )
- added new demo scenes for each respective functionality type
- few 'quality of life' improvements and usability fixes based on users requests

V 1.4.1 052017
- RAW format support also in the Editor
- iOS recording guide and fixes
- readme updated with general remarks on usability of FMOD and Unity versions
- startup sync to better synchronise user scripts with FMOD initialisation
- Unity audio sample rate compensation for stream sample rate bugfix for AudioStream component
- added AvailableOutputs() also to AudioSourceOutpuDevice allowing its better independent usage

V 1.5 052017
- finally support for Unity spatialisation
- simple minimal 3D demo scene
- few inconsistencies and limitations were resolved in connection to the above

V 1.5.1 062017
- AudioSourceOutputDevice hotfixes release:
	- fixed sample rate mismatch and consequent memory leak leading to sound degradation over time (regression from 1.4)
	- fix for Best latency DSP buffer size in Audio Settings
	- much smoother runtime device change without noticeable audio popping
	- error message instead of plain exception when AudioSourceOutputDevice is not initialized / startup have to be synchronized since 1.5 /

V 1.5.2 072017
- [Advanced] setting on AudioStream allows user to set "Stream Block Alignment" as workaround for audio files with unusually large tag blocks - typically e.g. mp3's with embedded artwork
- new low latency 2D input component AudioStreamInput2D + demo scene
- AudioSourceOutputDevice allows chaining of audio filters
- AudioSourceOutputDevice further fixes for empty clip on startup
- new event on stream tags/track change and better stream tags handling in general
- new GOAudioSaveToFile component allows automatic saving of audio being played on GO
- reorganized project files with more logical grouping (before upgrading it's probably good idea to delete existing version first)
- updated README && in Editor help texts

V 1.6 092017
- AudioSourceOutputDevice - added initialization based on currently selected output device sample rate
- added support for PCM8, PCM24, PCM32 and PCMFLOAT stream formats for AudioStream ( Unity AudioSource ) - note: this enables also playback of MIDI and modules audio files.
- added support for GoogleVR 3D spatializer on GameObjects via GVRSource and GVRSoundfield components.
-   currently playback via FMOD audio only ( no AudioSource )
-   room definition support, audio input GVR component, and integration with AudioSource planned.

V 1.7 112017
- changes for FMOD Studio Unity Integrations 1.10.00 (warning: Versions prior to 1.10.00 are no longer supported.)
- for tags support and 1.10.00 please see 'General Unity / mobile / FMOD plugin notes'
- AudioStreamInput2D has now hard dependency on AudioSource removed - you can mix input signal with e.g. AudioListener buffer
- Autodetect stream format option - it is default, but you *have* to select proper format on iOS
- fixes, optimizations and better exception handling
- due to minimal supported version of Unity on macOS's APFS being 5.5.4p5, AudioStream is submitted with this version

v 1.7.1 112017
- bugfix for AudioSource stopping imediately when set to (default) automatic start
- more startup stability fixes esp. for iOS
- new IcecastSource component and demo scene providing PCM and OGGVORBIS encoded stream from any AudioSource to an Icecast 2.4.0+ mountpoint (see README for details)

v 1.7.2 122017
- checked FMOD Studio 1.10.02 compatibility
- FMOD version used is available now and displayed in demo scenes
- updated automatic upgradable sources for Unity 2017.2 
- added separate input gain for audio input sources
- Fix for available recording devices on Android
- removed editor resources to have directivity texture computed instead of being textures composed for GoogleVR 3D sound
- new connectivity and starvation influencing advanced parameters for AudioStream* ( 'Initial Connection Retry Count' and 'Starving Retry Count'  )
- AudioStream - will now try to mute on starvation
- updated README iOS/Android background audio guides

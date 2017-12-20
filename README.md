# SERL-UnityGCS
Ground Control Station (User Interface Design) - Systems Engineering Research Laboratory

This GCS design is a lasting process dating back to mid-September, 2017.

It is a colaborative work that is being completed by the E2SH team in colaboration with Human Factors, Human Machine Teaming, and Control and Mapping teams. Conducted under the supervision of Seyed Sajjadi who is in charge of overal management and oversees operations of the GCS design.

Our current GCS desgin (3.0 - Dec. 2017) implents:
  - Live Video Feed
  - Live audio streaming (both sending and receiving)
  - Implementations for pubishing data to Ros Operating System (ROS) via RosBridge
  - Mock-up Occupancy Grid/map (Live Map feed coming in future release)
  - Teloperation commands for moving the Husky Bot via RosBrige
  
Due to Unity3d's outdated audio streaming assets, we had to utilize a resource called AudioStream 1.7.2, by Martin CvenGroš, which riquires FMOD, for both our input and output audio streams. Both input/ouput streams, stream and publish audio data to Icecast streams which we E2Sh manage. For full documentation and more on AudioStream 1.7.2, by Martin CvenGroš visit, https://www.assetstore.unity3d.com/en/#!/content/65411.

Rosbridge communications were accomplished by the help of the repository located at, https://github.com/2016UAVClass/Simulation-Unity3D. That project was originally inteded as a method of controlling the Turtle Sim simulator using Unity3d via RosBridge. We have used their libraries as tools so that we can communicate though RosBridge.

Our project contains various folders of which are needed for AudioStream, FMOD, and the RosBridge Library. You will find the scripts found Scripts folder to be those that implement the funtions mentioned above.

// GetImage.cs is responsible for grabing a tecture form ROSbridge's 
// video_websocket_server. The tecture corresponds to the live feed camera
// that is attached to the Husky bot in Gazebo. 
// After capturing the image, the image is displayed onto a Gameobject in Unity,
// for visability. 
// The image is refreshed at every frame providing an updated new image, thus giving
// a live video stream of the cameras on the Gazebo Husky bot.

using System.Collections;
using UnityEngine;

public class GetImage : MonoBehaviour {
	// Allows us to edit the url variable for editing though the inspector.
	public string Url = "https://www.w3schools.com/css/img_fjords.jpg";

	// At start of program the first image is captured.
	void Start ()
	{
		// Calls the funtion GetTexture to grab the Husky image.
		StartCoroutine(GetTexture());
	}
	
	// At every frame during program execut9ion, get a new image from ROSbridge video_websocket_server.
	void Update ()
	{
		// Calls the funtion GetTexture to grab the Husky image.
		StartCoroutine((GetTexture()));
	}
	
	// Handles getting. 
	IEnumerator GetTexture()
	{
		// Instantiate a Texture2D, and gives the 
		// dimensions and format of the texture.
		var tex = new Texture2D(4, 4, TextureFormat.DXT1, false);;
		
		// A WWW instance is given access to a website, in this case the link to the ROSbridge 
		// video_websocket_server image.
		var www = new WWW(Url);
		
		// Pause/wait for the site to respond with the image
		yield return www;
		
		//Loads the ROSbridge video_websocket_server husky image into the Textture2D 'tex'
		www.LoadImageIntoTexture(tex);
		
		// Grabs the image loaded in 'tex' and renders it for visualization in the Unity GameObject that 
		// the script is attached to. (allows for visualization)
		GetComponent<Renderer>().material.mainTexture = tex;
	}
}

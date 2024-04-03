using System;
using System.Text.RegularExpressions;

public class CPHInline
{
    public bool Execute()
    {
    	try {
			// OBS settings.
			int obsConnectionID = 0;        
			// Get inputs. 
			string newSceneName = args["obs.sceneName"].ToString();        
			// Validate Inputs. 
			if (string.IsNullOrEmpty(newSceneName))
			{
				CPH.LogDebug("Scene name is null or empty.");
				return false; // Return false if validation fails
			}                
			// Set Reaper Mic State. 
			SetReaperAudioState(newSceneName, obsConnectionID);
			return true;
		} catch (Exception ex) {
			CPH.LogDebug($"CS Error: Error occurred within main body: {ex.Message}");			
			return false; 
		}
    }
    
    private void SetReaperAudioState(string sceneName, int obsConnectionID)
    {        
    	try { 
			// Regex Strings. 
			string scenePrefixToCheck = @"^(00|01).*"; 
			string mutedMicScenesPattern = @"^(00|01)(?=(_tactical_pause|_starting_soon)).*$";        
        
			// OBS Scene Variables. 
			string audioSceneName = "03_Audio Settings - Separated"; 
			string reaperSource = "Reaper Audio"; 
			int micOn = 1; 
			int micOff = 0; 

			// Confirm this scene is one to check. 
			if (Regex.IsMatch(sceneName, scenePrefixToCheck))
			{
				// Check if this scene needs muting. 
				bool isMutedScene = Regex.IsMatch(sceneName, mutedMicScenesPattern);
            
				// Set Reaper Mic State based
				CPH.ObsSetSourceMuteState(audioSceneName, reaperSource, isMutedScene ? micOff : micOn, obsConnectionID);
			}     
			else
			{       
				CPH.LogDebug($"CS Information: Scene isn't one which needs to be checked. {sceneName}");
			}
		} catch (Exception ex) {
			CPH.LogDebug($"CS Error: Error occurred within SetReaperAudioState body: {ex.Message}");
			throw; 		
		}		
	}
}




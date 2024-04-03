using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

public class CPHInline
{
    // Dictionary mapping video names to file paths, used for direct invocation
    private Dictionary<string, string> videoNameMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        {"Gratitude", "gina_gratitude.mp4"},
        {"Bobbie Jean", "bob_mortimer_billie_jean.mp4"}
    };

    // Dictionary for channel point redemption video mappings
    private Dictionary<string, string> channelPointRedeemVideos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        {"Gratitude", "gina_gratitude.mp4"},
        {"Bobbie Jean", "bob_mortimer_billie_jean.mp4"}
    };

    public bool Execute()
    {
        // Initialize and get necessary variables from a helper method
        if (!Initialize(out int obsConnection, out string obsMediaScene, out string obsMediaSource, 
                        out string mediaFolderPath, out string initiator, out int separateCPFolderExists)) return false;

        // Determine if a separate folder for channel points should be used
        bool useCPFileFolder = initiator == "TwitchRewardRedemption" && separateCPFolderExists == 1;

        // If separate CP medial folder is marked as 1, try to resolve the folder. Default to main folder.         
        string mediaFileFolderPath = useCPFileFolder ? CPH.GetGlobalVar<string>("DMFL_CP_MEDIAFOLDERPATH", true) : mediaFolderPath;        
        if (string.IsNullOrEmpty(mediaFileFolderPath)) return LogError("Media folder path is null or empty.");

        // Get the redeem name based on the trigger type (e.g., Twitch redemption, command)
        string redeemName = GetRedeemName(initiator, mediaFileFolderPath, useCPFileFolder);
        if (string.IsNullOrEmpty(redeemName)) return false;

        // Resolve the full path to the media file
        string fullMediaFilePath = GetFullMediaFilePath(redeemName, mediaFileFolderPath, useCPFileFolder);
        if (string.IsNullOrEmpty(fullMediaFilePath) || !File.Exists(fullMediaFilePath)) return LogError("File not found or path is empty.");

        // Set the OBS media source file to the selected media file
        if (!SetObsMediaSourceFile(obsMediaScene, obsMediaSource, fullMediaFilePath, obsConnection)) return false;

        // Hide the media source before starting playback, then show (and thereby start) the media source
        CPH.ObsHideSource(obsMediaScene, obsMediaSource, obsConnection);
        CPH.Wait(10); 
        CPH.ObsShowSource(obsMediaScene, obsMediaSource, obsConnection);

        // Check if we should use existing saved durations. 
        string fileDurationPrefix = useCPFileFolder ? "DMFL_CP_FILE_DURATION_" : "DMFL_FILE_DURATION_";
        string callExistingTimersString = useCPFileFolder ? "DMFL_CP_CALL_EXISTINGTIMERS" : "DMFL_CALL_EXISTINGTIMERS";         
        string useExistingTimers = CPH.GetGlobalVar<string>(callExistingTimersString, true);
        bool useExistingTimerKnowledge = useExistingTimers.ToUpper() == "YES";
        int fileDuration = 0;

        // If so, look for an existing duration. 
        if (useExistingTimerKnowledge) { 
            fileDuration = CPH.GetGlobalVar<int>(fileDurationPrefix + redeemName, true);
        }
        
        // If we don't have one still, set one. 
        if (fileDuration == 0) { 
            if (!SetMediaDuration(obsMediaScene, obsMediaSource, redeemName, useCPFileFolder, obsConnection)) {
                return LogError("Failed to get or use media duration.");
            }      
            fileDuration = CPH.GetGlobalVar<int>(fileDurationPrefix + redeemName, true);
        }

        if (fileDuration > 0) {                    
            CPH.Wait(fileDuration);
            CPH.ObsHideSource(obsMediaScene, obsMediaSource, obsConnection);
        } else { 
            return LogError("No media duration known.");
        }
        return true;
    }

    private bool Initialize(out int obsConnection, out string obsMediaScene, out string obsMediaSource, out string mediaFolderPath, out string initiator, out int separateCPFolderExists)
    {
        obsConnection = -1;
        obsMediaScene = CPH.GetGlobalVar<string>("DMFL_SETUP_OBSMEDIAASCENE", true);
        obsMediaSource = CPH.GetGlobalVar<string>("DMFL_SETUP_OBSMEDIASOURCE", true);
        mediaFolderPath = CPH.GetGlobalVar<string>("DMFL_MAIN_MEDIAFOLDERPATH", true);
        initiator = args["__source"].ToString();
        int.TryParse(CPH.GetGlobalVar<string>("DMFL_SETUP_SEP_CP_FOLDER", true), out separateCPFolderExists);

        return int.TryParse(CPH.GetGlobalVar<string>("DMFL_SETUP_OBSCONNECTION", true), out obsConnection) 
                && obsConnection >= 0 
                && !string.IsNullOrEmpty(obsMediaScene) 
                && !string.IsNullOrEmpty(obsMediaSource) 
                && !string.IsNullOrEmpty(mediaFolderPath);
    }

    private string GetRedeemName(string initiator, string mediaFolderPath, bool useCPFileFolder)
    {   
        string keyName = initiator == "TwitchRewardRedemption" ? "rewardName" 
                       : initiator == "CommandTriggered" ? "commandName" : null;
        
        if (string.IsNullOrEmpty(keyName) || !args.ContainsKey(keyName)) {   
            return null;
        }

        string redeemName = args[keyName]?.ToString();

        string fileLocKeyPrefix = useCPFileFolder ? "DMFL_CP_FILE_LOC_" : "DMFL_FILE_LOC_";
        string useExistingFileLocKey = useCPFileFolder ? "DMFL_CP_CALL_EXISTINGLOC" : "DMFL_CALL_EXISTINGLOC";
        
        string useExistingFileLoc = CPH.GetGlobalVar<string>(useExistingFileLocKey, true);
        if (useExistingFileLoc?.ToUpper() == "YES") {
            string filePathExists = CPH.GetGlobalVar<string>(fileLocKeyPrefix + redeemName, true);
            if (!string.IsNullOrEmpty(filePathExists)) {
                return redeemName;
            }
        }

        var redeemVideos = useCPFileFolder ? channelPointRedeemVideos : videoNameMappings;
        if (redeemVideos.TryGetValue(redeemName, out string fileToPlay)) {
            string fullMediaFilePath = Path.Combine(mediaFolderPath, fileToPlay);
            CPH.SetGlobalVar(fileLocKeyPrefix + redeemName, fullMediaFilePath, true);
            return redeemName;
        } else {
            return null;
        }        

    }

    private string GetFullMediaFilePath(string redeemName, string mediaFolderPath, bool useCPFileFolder)
    {
        string fileMappingPrefix = useCPFileFolder ? "DMFL_CP_FILE_LOC_" : "DMFL_FILE_LOC_";
        return CPH.GetGlobalVar<string>(fileMappingPrefix + redeemName, true);
    }

    private bool SetObsMediaSourceFile(string obsMediaScene, string obsMediaSource, string fullMediaFilePath, int obsConnection)
    {
       try { 
            CPH.ObsSetMediaSourceFile(obsMediaScene, obsMediaSource, fullMediaFilePath, obsConnection);
            if (!CPH.ObsIsConnected(obsConnection)) {           
                return LogError("OBS WebSocket is not connected.");
            }

            string requestType = "GetInputSettings";
            JObject requestData = new JObject(); 
            requestData["inputName"] = obsMediaSource;       
            string obsSendRawResponse = CPH.ObsSendRaw(requestType, requestData.ToString(), obsConnection);

            return TryParseObsFileResponse(obsSendRawResponse, fullMediaFilePath);
        }
        catch (Exception ex) {
            return LogError("Exception occurred while setting the media source file in OBS.", ex);
        }
    }

    private bool TryParseObsFileResponse(string jsonResponse, string expectedFilePath)
    {
        try {
            JObject responseJson = JObject.Parse(jsonResponse);
            return responseJson["inputSettings"]?["local_file"]?.ToString() == expectedFilePath;
        }
        catch (Exception ex) {
            return LogError("JSON parsing error when verifying OBS source file update.", ex);
        }
    }

    private bool SetMediaDuration(string obsMediaScene, string obsMediaSource, string redeemName, bool useCPFileFolder, int obsConnection)
    {  	
        const int retryIntervalMS = 50; 
        const int timeoutMS = 10000; 
        int elapsedTime = 0; 

        string requestType = "GetMediaInputStatus";
        JObject requestData = new JObject(); 
        requestData["inputName"] = obsMediaSource;         
        
        if (!CPH.ObsIsConnected(obsConnection)) {           
            return LogError("OBS WebSocket is not connected.");
        }

        string obsSendRawResponse = string.Empty;
        JObject responseJson = null;
        bool responseReceived = false;
        int mediaDuration = 0;
        
		while (elapsedTime < timeoutMS && !responseReceived) {
			obsSendRawResponse = CPH.ObsSendRaw(requestType, requestData.ToString(), obsConnection);
			if (!string.IsNullOrEmpty(obsSendRawResponse)) {
				responseJson = JObject.Parse(obsSendRawResponse);
				if (responseJson.TryGetValue("mediaDuration", out JToken mediaDurationToken)) {
					mediaDuration = mediaDurationToken.Value<int>();
					if (mediaDuration > 0) {
						responseReceived = true;
						break;
					}
				}
			}
			CPH.Wait(retryIntervalMS);
			elapsedTime += retryIntervalMS;
		}

        if (!responseReceived) {
            return LogError("Response not received within the expected timeout.");
        }
        if (mediaDuration <= 0) {
            return LogError("Media duration is non-positive or not found.");
        }
            
        string fileDurationPrefix = useCPFileFolder ? "DMFL_CP_FILE_DURATION_" : "DMFL_FILE_DURATION_";
        string g2PrefixWithFilename = fileDurationPrefix + redeemName;

        CPH.SetGlobalVar(g2PrefixWithFilename, mediaDuration, true);
        return true;    
    }

    private bool LogError(string errorMessage, Exception ex = null)
    {
        string fullMessage = $"Error: {errorMessage}";
        if (ex != null) fullMessage += $", Exception: {ex.Message}";

        string chatDebugMessageString = CPH.GetGlobalVar<string>("DMFL_SETUP_CHATDEBUG", true);
        if (chatDebugMessageString == "On") CPH.SendMessage(fullMessage);

        CPH.SendMessage(fullMessage);
        CPH.LogError(fullMessage);
        return false;
    }

}
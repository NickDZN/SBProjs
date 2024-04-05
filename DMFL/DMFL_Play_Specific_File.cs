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
        {"Bobbie Jean", "bob_mortimer_billie_jean.mp4"},
        {"TAPFOD", "Wykah_FuckOffDiszen_Terrance_and_Phil.mp4"}
    };

    // Dictionary for channel point redemption video mappings
    private Dictionary<string, string> channelPointRedeemVideos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        {"Gratitude", "gina_gratitude.mp4"},
        {"Bobbie Jean", "bob_mortimer_billie_jean.mp4"}
    };

    /// <summary>
    /// Main execution method for controlling flow and playing a media file based on the request recieved.
    /// </summary>
    /// 
    /// <returns> True if execution was successful, false if not.</returns>
    public bool Execute() 
    {   
        //Check process isn't already running.
        string dmflStatusGlobal = "DMFL_STATUS_NOW_PLAYING"; 
        int processRunning = CPH.GetGlobalVar<int>("DMFL_SETUP_SEP_CP_FOLDER", false);
        if (processRunning == 1) {
            return false; 
        }

        // Attempt to initialise. 
        if (!Initialize(out int obsConnection, out string obsMediaScene, out string obsMediaSource, 
                        out string mediaFolderPath, out string initiator, out int separateCPFolderExists)) return false;

        if (!ValidateSourceIsInCurrentScene(obsMediaSource, obsConnection)) { 
            return false; 
        }

        // Set in progress marker.         
        CPH.SetGlobalVar(dmflStatusGlobal, 1, false);

        // Determine if a separate folder for channel points should be used
        bool useCPFileFolder = initiator == "TwitchRewardRedemption" && separateCPFolderExists == 1;

        // If separate CP medial folder is marked as 1, try to resolve the folder. Default to main folder.         
        string mediaFileFolderPath = useCPFileFolder ? CPH.GetGlobalVar<string>("DMFL_CP_MEDIAFOLDERPATH", true) : mediaFolderPath;        
        if (string.IsNullOrEmpty(mediaFileFolderPath)) return LogError("Media folder path is null or empty.");
        string fileName; 

        // Turn source off if on. 
        if (!SetOBSSourceOff(obsMediaScene, obsMediaSource, obsConnection)) { 
            return false; 
        }

        // Get the redeem name based on the trigger type (e.g., Twitch redemption, command), and file path. 
        var mediaDetails = GetMediaFileDetails(initiator, mediaFileFolderPath, useCPFileFolder);
        if (mediaDetails.FileName == null && mediaDetails.FileFullPath == null) {
            return LogError("No media details found.");
        } else { 
            if (!File.Exists(mediaDetails.FileFullPath)) {
                return LogError($"File not found at path: {mediaDetails.FileFullPath}");
            }
            fileName = mediaDetails.FileName;
        }

        // Set the OBS media source file to the selected media file
        if (!SetObsMediaSourceFile(obsMediaScene, obsMediaSource, mediaDetails.FileFullPath, obsConnection)) return false;

        CPH.ObsShowSource(obsMediaScene, obsMediaSource, obsConnection);

        // Check if we should use existing saved durations. 
        string fileDurationPrefix = useCPFileFolder ? "DMFL_CP_FILE_DURATION_" : "DMFL_FILE_DURATION_";
        string callExistingTimersString = useCPFileFolder ? "DMFL_CP_CALL_EXISTINGTIMERS" : "DMFL_CALL_EXISTINGTIMERS";         
        string useExistingTimers = CPH.GetGlobalVar<string>(callExistingTimersString, true);
        bool useExistingTimerKnowledge = useExistingTimers.ToUpper() == "YES";
        int fileDuration = 0;

        // If so, look for an existing duration. 
        if (useExistingTimerKnowledge) { 
            fileDuration = CPH.GetGlobalVar<int>(fileDurationPrefix + fileName, true);
        }
        
        // If we don't have one still, set one. 
        if (fileDuration == 0) { 
            if (!SetMediaDuration(obsMediaScene, obsMediaSource, fileName, useCPFileFolder, obsConnection)) {
                return LogError("Failed to get or use media duration.");
            }      
            fileDuration = CPH.GetGlobalVar<int>(fileDurationPrefix + fileName, true);
        }

        if (fileDuration > 0) {             
            CPH.Wait(fileDuration);
            CPH.ObsHideSource(obsMediaScene, obsMediaSource, obsConnection);
        } else { 
            return LogError("No media duration known.");
        }
        
        CPH.SetGlobalVar(dmflStatusGlobal, 0, false);
        return true;
    }

    /// <summary>
    /// Initializes necessary variables from global settings and arguments for the execution.
    /// </summary>
    /// 
    /// <param name="obsConnection">            Out Param: OBS connection ID.</param>
    /// <param name="obsMediaScene">            Out Param: OBS media scene name.</param>
    /// <param name="obsMediaSource">           Out Param: OBS media source name.</param>
    /// <param name="mediaFolderPath">          Out Param: Main media folder path.</param>
    /// <param name="initiator">                Out Param: Command Type.</param>
    /// <param name="separateCPFolderExists">   Out Param: Indicates if a separate folder for channel points is used.</param>
    /// 
    /// <returns> True if initialization is successful and all necessary variables are set, false otherwise. </returns>
    private bool Initialize(out int obsConnection, out string obsMediaScene, out string obsMediaSource, out string mediaFolderPath, out string initiator, out int separateCPFolderExists)
    {
        // Initialize output variables to their default values
        obsConnection = -1;
        separateCPFolderExists = 0;

        // Attempt to parse OBS connection as an integer
        bool isObsConnectionValid = int.TryParse(CPH.GetGlobalVar<string>("DMFL_SETUP_OBSCONNECTION", true), out obsConnection);
        bool connected = CheckObsConnection(obsConnection);

        // Retrieve other variables from global settings
        bool isSeparateCPFolderParsed = int.TryParse(CPH.GetGlobalVar<string>("DMFL_SETUP_SEP_CP_FOLDER", true), out separateCPFolderExists);
        obsMediaScene = CPH.GetGlobalVar<string>("DMFL_SETUP_OBSMEDIAASCENE", true);
        obsMediaSource = CPH.GetGlobalVar<string>("DMFL_SETUP_OBSMEDIASOURCE", true);
        mediaFolderPath = CPH.GetGlobalVar<string>("DMFL_MAIN_MEDIAFOLDERPATH", true);
        initiator = args["__source"].ToString();

        if (!connected) { 
            return LogError("OBS Not Connected.");
        }

        // Sanitize and return: ensure all required fields are valid
        return isObsConnectionValid 
                && connected 
                && obsConnection >= 0 
                && !string.IsNullOrEmpty(obsMediaScene) 
                && !string.IsNullOrEmpty(obsMediaSource) 
                && !string.IsNullOrEmpty(mediaFolderPath)
                && !string.IsNullOrEmpty(initiator)
                && separateCPFolderExists >= 0; // Assuming non-zero means true/exists
    }

    /// <summary>
    /// Validate the source for the media exists on this scene. Without this the 
    /// duration details wont be detectable. 
    /// </summary>
    /// 
    /// <param name="obsMediaSource">   The OBS Source the media is due to play on.</param>
    /// <param name="obsConnection">    The OBS WebSocket connection ID to check.</param>
    /// 
    /// <returns> True if the connection is active, false otherwise. </returns>
    private bool ValidateSourceIsInCurrentScene(string obsMediaSource, int obsConnection)
    {
        // Setup Vars. 
        const int retryIntervalMS = 500;
        const int timeoutMS = 5000;
        int elapsedTime = 0;
        string currentSceneName = null;

        // Get the current scene
        JObject getCurrentSceneRequestData = new JObject();
        while (string.IsNullOrEmpty(currentSceneName) && elapsedTime < timeoutMS) {
            string getCurrentSceneResponse = CPH.ObsSendRaw("GetCurrentProgramScene", getCurrentSceneRequestData.ToString(), obsConnection);

            if (!string.IsNullOrEmpty(getCurrentSceneResponse)) {
                JObject getCurrentSceneResponseJson = JObject.Parse(getCurrentSceneResponse);
                currentSceneName = getCurrentSceneResponseJson["currentProgramSceneName"]?.ToString();
                break; // Exit loop if current scene name is obtained
            } else {
                System.Threading.Thread.Sleep(retryIntervalMS); // Wait before retrying
                elapsedTime += retryIntervalMS;
            }
        }

        // Exit if not found. 
        if (string.IsNullOrEmpty(currentSceneName)) {
            return LogError("Failed to get the current program scene or operation timed out.");
        }

        // Reset Vars. 
        elapsedTime = 0;
        JObject getSceneItemsRequestData = new JObject { ["sceneName"] = currentSceneName };
        JArray sceneItems = null;

        // Get array of all items on current scene for comparison. 
        while (sceneItems == null && elapsedTime < timeoutMS) {    
            string getSceneItemsResponse = CPH.ObsSendRaw("GetSceneItemList", getSceneItemsRequestData.ToString(), obsConnection);

            if (!string.IsNullOrEmpty(getSceneItemsResponse)) {
                JObject getSceneItemsResponseJson = JObject.Parse(getSceneItemsResponse);
                sceneItems = (JArray)getSceneItemsResponseJson["sceneItems"];
                break; 
            }
            System.Threading.Thread.Sleep(retryIntervalMS); 
            elapsedTime += retryIntervalMS;
        }

        if (sceneItems == null) {
            return LogError($"Failed to get scene items for scene: {currentSceneName} or operation timed out.");
        }

        // Check if obsMediaSource is in the list of source names
        foreach (var item in sceneItems) {
            string sourceName = (string)item["sourceName"];
            if (!string.IsNullOrEmpty(sourceName) && sourceName.Equals(obsMediaSource, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }
        return LogError($"Source: {obsMediaSource} not found on this scene. Unable to play.");
    }

    /// <summary>
    /// Sets the OBS media source off if it's currently on. 
    /// </summary>
    /// 
    /// <param name="obsMediaScene">        The name of the OBS scene containing the media source.</param>
    /// <param name="obsMediaSource">       The name of the OBS media source to be updated.</param>
    /// <param name="obsConnection">        The OBS WebSocket connection ID.</param>
    /// 
    /// <returns>True if the media source file was successfully set, false if an error occurred.</returns>
    private bool SetOBSSourceOff(string obsMediaScene, string obsMediaSource, int obsConnection) {

        //Set Vars.
        const int retryIntervalMS = 50; 
        const int timeoutMS = 1000; 
        int elapsedTime = 0;  

        // Loop over and turn off source if it's on. 
        while (CPH.ObsIsSourceVisible(obsMediaScene, obsMediaSource, obsConnection) && elapsedTime < timeoutMS) { 
            CPH.ObsHideSource(obsMediaScene, obsMediaSource, obsConnection);
            CPH.Wait(retryIntervalMS);  
            elapsedTime += retryIntervalMS; 
        };

        if (elapsedTime >= timeoutMS) { 
            return LogError($"Unable to turn off source to start the process: {obsMediaSource}");
        };
        return true; 
    }

    /// <summary>
    /// Determines the media file to play based on the action initiator and configured paths, returning the file name and full path.
    /// </summary>
    /// 
    /// <param name="initiator">            The action initiator type (e.g., TwitchRewardRedemption, CommandTriggered).</param>
    /// <param name="mediaFolderPath">      Path to the folder containing media files.</param>
    /// <param name="useCPFileFolder">      Indicates if a separate folder for channel points is used.</param>
    /// 
    /// <returns> A tuple containing the media file name and its full path. </returns>
    private (string FileName, string FileFullPath) GetMediaFileDetails(string initiator, string mediaFolderPath, bool useCPFileFolder)
    {   

        // Determines whether this was initiated from a !command or Channel Point Redeem. 
        string keyName = initiator == "TwitchRewardRedemption" ? "rewardName" 
                       : initiator == "CommandTriggered" ? "commandName" : null;

        // Determine whether to store redeem name, or file name. This will help with random file loader too. 
        bool shouldStoreFileName = initiator == "CommandTriggered" || (initiator == "TwitchRewardRedemption" && !useCPFileFolder);
        
        // Confirm  key name isn't null and the args contains the key.
        if (string.IsNullOrEmpty(keyName) || !args.ContainsKey(keyName)) {   
            return (null, null);
        }

        // Determine the redeem name, and the storage value. 
        string redeemName = args[keyName]?.ToString();

        // Determines which collection of redeemable videos to use based on whether CP file folders are used.sdd
        var redeemVideos = useCPFileFolder ? channelPointRedeemVideos : videoNameMappings;

        // Find media file mapped to redeem name. 
        if (redeemVideos.TryGetValue(redeemName, out string fileToPlay)) {
            string fullMediaFilePath = Path.Combine(mediaFolderPath, fileToPlay);
            string fileIdentifier = shouldStoreFileName ? fileToPlay : redeemName;

            // Set the prefix for storing file location global and the global to check if an existing file location should be used.
            string fileLocKeyPrefix = useCPFileFolder ? "DMFL_CP_FILE_LOC_" : "DMFL_FILE_LOC_";

            // Store the full media file path in a global variable using the appropriate key.
            CPH.SetGlobalVar(fileLocKeyPrefix + fileIdentifier, fullMediaFilePath, true);
            return (fileIdentifier, fullMediaFilePath);
        }
        return (null, null); 
    }

    /// <summary>
    /// Sets the OBS media source file to a specified media file.
    /// </summary>
    /// 
    /// <param name="obsMediaScene">        The name of the OBS scene containing the media source.</param>
    /// <param name="obsMediaSource">       The name of the OBS media source to be updated.</param>
    /// <param name="fullMediaFilePath">    The full path of the media file to be set as the source.</param>
    /// <param name="obsConnection">        The OBS WebSocket connection ID.</param>
    /// 
    /// <returns>True if the media source file was successfully set, false if an error occurred.</returns>
    private bool SetObsMediaSourceFile(string obsMediaScene, string obsMediaSource, string fullMediaFilePath, int obsConnection)
    {
       try { 
            // Check connection first. 
            if (!CheckObsConnection(obsConnection)) return false;

            // Set the OBS file for this source. 
            CPH.ObsSetMediaSourceFile(obsMediaScene, obsMediaSource, fullMediaFilePath, obsConnection);        

            //Build up an OBS raw request to confirm current file for the source. 
            string requestType = "GetInputSettings";
            JObject requestData = new JObject(); 
            requestData["inputName"] = obsMediaSource;       

            // Send request for current file. 
            string obsSendRawResponse = CPH.ObsSendRaw(requestType, requestData.ToString(), obsConnection);
            
            // Parse the file response. 
            return TryParseObsFileResponse(obsSendRawResponse, fullMediaFilePath);
        }
        catch (Exception ex) {
            return LogError("Exception occurred while setting the media source file in OBS.", ex);
        }
    }

    /// <summary>
    /// Attempts to parse the OBS WebSocket response to verify the media file was correctly set.
    /// </summary>
    /// 
    /// <param name="jsonResponse">         The JSON response from OBS WebSocket.</param>
    /// <param name="expectedFilePath">     The expected file path of the media file.</param>
    /// 
    /// <returns> True if the actual file path matches the expected file path, false otherwise. </returns>
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

    /// <summary>
    /// Sets the duration for the media file being played.
    /// </summary>
    /// 
    /// <param name="obsMediaScene">            The OBS scene containing the media source.</param>
    /// <param name="obsMediaSource">           The OBS media source name.</param>
    /// <param name="mediaFileIdentifier">      The name of the media file or redeem action.</param>
    /// <param name="useCPFileFolder">          Indicates if a separate folder for channel points is used.</param>
    /// <param name="obsConnection">            The OBS WebSocket connection ID.</param>
    /// 
    /// <returns> True if the media duration is successfully set, false otherwise. </returns>
    private bool SetMediaDuration(string obsMediaScene, string obsMediaSource, string mediaFileIdentifier, bool useCPFileFolder, int obsConnection)
    {
        if (!CheckObsConnection(obsConnection)) return false;

        const int retryIntervalMS = 500;
        const int timeoutMS = 5000;
        int elapsedTime = 0;
        int mediaDuration = 0;
        bool mediaDurationRetrieved = false;

        CPH.SendMessage("SMD1: ");

        JObject requestData = new JObject { ["inputName"] = obsMediaSource };

        CPH.SendMessage("SMD2: ");

        while (elapsedTime < timeoutMS && !mediaDurationRetrieved) {
            string obsSendRawResponse = CPH.ObsSendRaw("GetMediaInputStatus", requestData.ToString(), obsConnection);
            CPH.SendMessage("SMD3: ");
            if (TryParseMediaDuration(obsSendRawResponse, out mediaDuration)) {
                CPH.SendMessage("SMD3a: ");
                mediaDurationRetrieved = true;
            } else {
                elapsedTime += retryIntervalMS;
                CPH.Wait(retryIntervalMS);
                CPH.SendMessage("SMD5: ");
            }
        }
        if (!mediaDurationRetrieved || mediaDuration <= 0) {
            return LogError("Failed to retrieve a valid media duration.");
        }
        string fileDurationPrefix = useCPFileFolder ? "DMFL_CP_FILE_DURATION_" : "DMFL_FILE_DURATION_";
        CPH.SetGlobalVar(fileDurationPrefix + mediaFileIdentifier, mediaDuration, true);

        return true;
    }

    /// <summary>
    /// Parse Media Duration. Helper method for setMediaDuration.
    /// Moved into own method to keep complexity down of source method.  
    /// </summary>
    /// 
    /// <param name="obsResponse">      The JSON response from OBS as a string.</param>
    /// <param name="mediaDuration">    OUT: The parsed media duration in milliseconds </param>
    /// 
    /// <returns> True if the connection is active, false otherwise. </returns>
    private bool TryParseMediaDuration(string obsResponse, out int mediaDuration) {
        try {
            JObject responseJson = JObject.Parse(obsResponse);
            if (responseJson.TryGetValue("mediaDuration", out JToken durationToken)) {
                mediaDuration = durationToken.Value<int>();
                return mediaDuration > 0;
            }
        } catch (Exception ex) {
            LogError($"Error parsing OBS response: {ex.Message}", ex, false);
        }
        mediaDuration = 0;
        return false;
    }

    /// <summary>
    /// Centralised method to checks if the OBS WebSocket connection is currently active.
    /// </summary>
    /// 
    /// <param name="obsConnection">    The OBS WebSocket connection ID to check.</param>
    /// 
    /// <returns> True if the connection is active, false otherwise. </returns>
    private bool CheckObsConnection(int obsConnection)
    {
        if (!CPH.ObsIsConnected(obsConnection)) {
            return LogError("OBS WebSocket is not connected.");
        }
        return true;
    }

    /// <summary>
    /// Logs any error messages, optionally sending them to chat too. 
    /// </summary>
    /// 
    /// <param name="errorMessage">     The error message to log.</param>
    /// <param name="ex">               Optional. Exception associated with the error.</param>
    /// <param name="critical">         Optional. Indicates whether the error will terminate execution.</param>
    /// 
    /// <returns> Always returns false as the script has failed. </returns>
    private bool LogError(string errorMessage, Exception ex = null, bool critical = true)
    {
        string fullMessage = $"Error: {errorMessage}";
        if (ex != null) fullMessage += $", Exception: {ex.Message}";

        string chatDebugMessageString = CPH.GetGlobalVar<string>("DMFL_SETUP_CHATDEBUG", true);
        if (chatDebugMessageString == "Yes") CPH.SendMessage(fullMessage);

        CPH.LogError(fullMessage);

        if (critical) { 
            CPH.SetGlobalVar("DMFL_STATUS_NOW_PLAYING", 0, false);
        }
        return false;
    }
}
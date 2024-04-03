using System;
using System.Collections.Generic;

public class CPHInline
{
    public bool Execute()
    {
    	string currentScene = args["obs.sceneName"].ToString();
    	CPH.SendMessage(currentScene);
        List<string> scenesRewardsAreEnabled = new List<string> { "00_tactical_pause", "01_Game_Fullscreen_Ultrawide", "01_Game_FullScrean_Cropped" };
        List<string> rewardsToAlter = new List<string> { "Gratitude", "Crimewatch", "PixelMan" };
        
        // Correctly call TwitchGetRewards and store the result in a variable
        List<TwitchReward> rewards = CPH.TwitchGetRewards();
                      
        // Check if this scene is meant to have the CPRedeem Active. 
        if (scenesRewardsAreEnabled.Contains(currentScene)) {
            
            // Check each reward. 
            foreach (var reward in rewards) {
                // Check if this is a reward to alter. 
                if (rewardsToAlter.Contains(reward.Title)) {
                    // Confirm Streamerbot can alter it, the redeem is enabled, and it is currently paused.
                    if (reward.IsOurs && reward.Enabled && reward.Paused) {
                        CPH.UnPauseReward(reward.Id);
                    }
                }
            }
        // Check if this scene is meant to have the CPRedeem paused.                 
        } else { 
            // Check each reward. 
            foreach (var reward in rewards) {
                // Check if this is a reward to alter. 
                if (rewardsToAlter.Contains(reward.Title)) {
                    // Confirm Streamerbot can alter it, the redeem is enabled, and it is currently unpaused.
                    if (reward.IsOurs && reward.Enabled && !reward.Paused) {
                        CPH.PauseReward(reward.Id);
                    }
                }
            }
        }        
        return true;         
    }
}
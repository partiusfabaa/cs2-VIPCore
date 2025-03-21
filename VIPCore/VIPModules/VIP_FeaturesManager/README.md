# Allows you to manage features

# vip_featuresmanager.json

```json
{
  "Jumps": {                      // feature name
    "DefaultState": 2,            // 0 - enabled, 1 - disabled, 2 - no access
    "Rounds": [2, 3, 5, 10],      // in which rounds the feature will be available
    "DisableOnPistolRound": true, // disable this feature in the first rounds?
    "DisableOnWarmup": true       // to disable the feature during warmup?
  }
}
```
 

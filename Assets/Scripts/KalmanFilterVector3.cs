using UnityEngine;

/// <summary>
/// Kalman Filter for Vector3 - Smooth tracking for 3D positions
/// Used for smooth hand/wrist tracking without jitter
/// Based on standard Kalman filter algorithm
/// </summary>
public class KalmanFilterVector3
{
    // Kalman filter parameters
    private float Q; // Process noise covariance
    private float R; // Measurement noise covariance
    private float P; // Estimation error covariance
    private float K; // Kalman gain
    
    // Filtered values for each axis
    private Vector3 filteredValue;
    private bool initialized = false;
    
    /// <summary>
    /// Create a new Kalman filter for Vector3
    /// </summary>
    /// <param name="processNoise">Q - Process noise (0.001 to 0.1, lower = smoother but more lag)</param>
    /// <param name="measurementNoise">R - Measurement noise (0.01 to 1.0, higher = smoother but more lag)</param>
    public KalmanFilterVector3(float processNoise = 0.01f, float measurementNoise = 0.1f)
    {
        Q = processNoise;
        R = measurementNoise;
        P = 1.0f;
        K = 0.0f;
        filteredValue = Vector3.zero;
        initialized = false;
    }
    
    /// <summary>
    /// Update the filter with a new measurement
    /// </summary>
    /// <param name="measurement">Raw position from tracking</param>
    /// <returns>Filtered (smoothed) position</returns>
    public Vector3 Update(Vector3 measurement)
    {
        if (!initialized)
        {
            // First measurement - initialize with raw value
            filteredValue = measurement;
            initialized = true;
            return filteredValue;
        }
        
        // Apply Kalman filter to each axis independently
        filteredValue.x = UpdateAxis(filteredValue.x, measurement.x);
        filteredValue.y = UpdateAxis(filteredValue.y, measurement.y);
        filteredValue.z = UpdateAxis(filteredValue.z, measurement.z);
        
        return filteredValue;
    }
    
    /// <summary>
    /// Apply Kalman filter to a single axis
    /// </summary>
    private float UpdateAxis(float previousValue, float measurement)
    {
        // Prediction step
        float predictedEstimate = previousValue;
        float predictedP = P + Q;
        
        // Update step
        K = predictedP / (predictedP + R);
        float filteredEstimate = predictedEstimate + K * (measurement - predictedEstimate);
        P = (1.0f - K) * predictedP;
        
        return filteredEstimate;
    }
    
    /// <summary>
    /// Reset the filter (use when tracking is lost and reacquired)
    /// </summary>
    public void Reset()
    {
        initialized = false;
        P = 1.0f;
        filteredValue = Vector3.zero;
    }
    
    /// <summary>
    /// Reset the filter with a specific starting value
    /// </summary>
    public void Reset(Vector3 startValue)
    {
        filteredValue = startValue;
        initialized = true;
        P = 1.0f;
    }
    
    /// <summary>
    /// Get the current filtered value without updating
    /// </summary>
    public Vector3 GetFilteredValue()
    {
        return filteredValue;
    }
    
    /// <summary>
    /// Check if filter has been initialized
    /// </summary>
    public bool IsInitialized()
    {
        return initialized;
    }
    
    /// <summary>
    /// Update filter parameters on the fly
    /// </summary>
    public void SetParameters(float processNoise, float measurementNoise)
    {
        Q = processNoise;
        R = measurementNoise;
    }
}

/// <summary>
/// Alternative: Simple moving average filter (lighter than Kalman)
/// Use this if Kalman is too heavy or you want simpler smoothing
/// </summary>
public class MovingAverageVector3
{
    private Vector3[] buffer;
    private int bufferSize;
    private int currentIndex = 0;
    private int filledCount = 0;
    
    public MovingAverageVector3(int windowSize = 5)
    {
        bufferSize = Mathf.Max(1, windowSize);
        buffer = new Vector3[bufferSize];
    }
    
    public Vector3 Update(Vector3 newValue)
    {
        buffer[currentIndex] = newValue;
        currentIndex = (currentIndex + 1) % bufferSize;
        
        if (filledCount < bufferSize)
            filledCount++;
        
        // Calculate average
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < filledCount; i++)
        {
            sum += buffer[i];
        }
        
        return sum / filledCount;
    }
    
    public void Reset()
    {
        currentIndex = 0;
        filledCount = 0;
    }
}

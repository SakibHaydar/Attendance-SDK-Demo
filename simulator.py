import requests
import time

# Update this with your actual URL (check AdmsPushApi/Properties/launchSettings.json)
SERVER_URL = "http://localhost:5122/iclock/cdata" 
DEVICE_SN = "SIMULATOR_001"

def simulate_swipe(user_id):
    timestamp = time.strftime("%Y-%m-%d %H:%M:%S")
    # ZKTeco format: ID \t Timestamp \t Status \t VerifyMode \t ...
    log_data = f"{user_id}\t{timestamp}\t0\t1\t0\t0\n"
    
    print(f"Pushing swipe for User {user_id}...")
    try:
        response = requests.post(f"{SERVER_URL}?SN={DEVICE_SN}", data=log_data)
        if response.text.strip() == "OK":
            print("Successfully pushed to server.")
        else:
            print(f"Server responded with: {response.text}")
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    print(f"Starting Simulator for SN: {DEVICE_SN}")
    # 1. Perform Handshake
    try:
        requests.get(f"{SERVER_URL}?SN={DEVICE_SN}")
    except Exception as e:
        print(f"Handshake failed: {e}")
    
    # 2. Simulate some swipes
    simulate_swipe("101")
    time.sleep(2)
    simulate_swipe("102")

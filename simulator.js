// Node.js Simulator for ADMS Push SDK
// Run with: node simulator.js

const SERVER_URL = "http://localhost:5122/iclock/cdata";
const DEVICE_SN = "SIMULATOR_001";

async function simulateSwipe(userId) {
    const timestamp = new Date().toISOString().replace(/T/, ' ').replace(/\..+/, '');
    // ZKTeco format: ID \t Timestamp \t Status \t VerifyMode \t ...
    const logData = `${userId}\t${timestamp}\t0\t1\t0\t0\n`;

    console.log(`Pushing swipe for User ${userId}...`);
    try {
        const response = await fetch(`${SERVER_URL}?SN=${DEVICE_SN}`, {
            method: 'POST',
            body: logData
        });
        const text = await response.text();
        if (text.trim() === "OK") {
            console.log("Successfully pushed to server.");
        } else {
            console.log(`Server responded with: ${text}`);
        }
    } catch (e) {
        console.error(`Error: ${e.message}`);
    }
}

async function start() {
    console.log(`Starting Node.js Simulator for SN: ${DEVICE_SN}`);

    // 1. Perform Handshake
    try {
        await fetch(`${SERVER_URL}?SN=${DEVICE_SN}`);
    } catch (e) {
        console.error(`Handshake failed: ${e.message}`);
    }

    // 2. Simulate some swipes
    await simulateSwipe("101");
    setTimeout(async () => {
        await simulateSwipe("102");
    }, 2000);
}

start();

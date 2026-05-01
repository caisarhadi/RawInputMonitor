let ws;
const dashboard = document.getElementById('dashboard');
const statusDiv = document.getElementById('status');

function connect() {
    ws = new WebSocket('ws://localhost:9100/ws');

    ws.onopen = () => {
        statusDiv.textContent = 'Connected';
        statusDiv.style.color = 'var(--status-connected)';
    };

    ws.onmessage = (event) => {
        const msg = JSON.parse(event.data);
        if (msg.type === 'deviceList') {
            renderDeviceList(msg.devices);
        } else if (msg.type === 'input') {
            updateInput(msg);
        }
    };

    ws.onclose = () => {
        statusDiv.textContent = 'Disconnected. Reconnecting...';
        statusDiv.style.color = 'var(--status-disconnected)';
        setTimeout(connect, 2000);
    };
}

function renderDeviceList(devices) {
    dashboard.innerHTML = '';
    devices.forEach(d => {
        const card = document.createElement('div');
        card.className = 'device-card';
        card.id = `device-${d.deviceId.replace(':', '-')}`;
        
        card.innerHTML = `
            <div class="device-header">
                <div class="device-title">${d.name} <span style="font-size:0.7em;color:#888;">[${d.deviceId}]</span></div>
                <div class="device-status ${d.connected ? 'status-connected' : 'status-disconnected'}"></div>
            </div>
            <div class="device-channels" id="channels-${d.deviceId.replace(':', '-')}"></div>
        `;
        dashboard.appendChild(card);
    });
}

function updateInput(input) {
    const channelsDiv = document.getElementById(`channels-${input.deviceId.replace(':', '-')}`);
    if (!channelsDiv) return;

    let containerId = `container-${input.deviceId.replace(':', '-')}-${input.sourceType}`;
    let container = document.getElementById(containerId);
    
    if (!container) {
        container = document.createElement('div');
        container.id = containerId;
        container.className = 'channel-group';
        
        const title = document.createElement('div');
        title.className = 'channel-group-title';
        title.textContent = input.sourceType === 'HID_BIT' ? 'Buttons (Bits)' : 'Axes / Values (Bytes)';
        
        container.appendChild(title);
        channelsDiv.appendChild(container);
    }

    const channelId = `channel-${input.deviceId.replace(':', '-')}-${input.channel.replace(/[^a-zA-Z0-9]/g, '_')}`;
    let row = document.getElementById(channelId);

    if (!row) {
        row = document.createElement('div');
        row.className = 'channel-row';
        row.id = channelId;
        row.innerHTML = `
            <span class="channel-name">${input.channel}</span>
            <span class="channel-value" id="val-${channelId}">0</span>
            <span class="channel-raw" id="raw-${channelId}" style="font-size: 0.8em; color: #666; margin-left: 15px;">[Raw: 0]</span>
        `;
        container.appendChild(row);
    }

    const valEl = document.getElementById(`val-${channelId}`);
    const rawEl = document.getElementById(`raw-${channelId}`);
    
    valEl.textContent = input.value;
    rawEl.textContent = `[Raw: ${input.raw}]`;
    
    valEl.style.color = '#fff';
    valEl.style.textShadow = '0 0 10px var(--accent)';
    clearTimeout(valEl.timeoutId);
    valEl.timeoutId = setTimeout(() => {
        valEl.style.color = 'var(--accent)';
        valEl.style.textShadow = 'none';
    }, 150);
}

connect();

let ws;
const dashboard = document.getElementById('dashboard');
const statusDiv = document.getElementById('status');

// Define display order for known channels so they always appear in a logical sequence
const CHANNEL_ORDER = {
    // SpaceMouse 6DOF
    'TX': 10, 'TY': 11, 'TZ': 12,
    'RX': 13, 'RY': 14, 'RZ': 15,
    // SpaceMouse Buttons
    'Button_1': 20, 'Button_2': 21, 'Button_3': 22, 'Button_4': 23,
    // Slimblade
    'X': 30, 'Y': 31, 'Twist_Scroll': 32,
    'Button_BottomLeft': 40, 'Button_BottomRight': 41,
    'Button_TopLeft': 42, 'Button_TopRight': 43, 'Button_Extra': 44,
};

function getChannelOrder(channel) {
    if (CHANNEL_ORDER[channel] !== undefined) return CHANNEL_ORDER[channel];
    // For numbered buttons (Button_5, Button_6, etc.), parse the number
    const btnMatch = channel.match(/^Button_(\d+)$/);
    if (btnMatch) return 20 + parseInt(btnMatch[1]);
    // Unknown channels go to the end, sorted alphabetically
    return 900;
}

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
        row.dataset.channel = input.channel;
        row.innerHTML = `
            <span class="channel-name">${input.channel}</span>
            <span class="channel-value" id="val-${channelId}">0</span>
            <span class="channel-raw" id="raw-${channelId}" style="font-size: 0.8em; color: #666; margin-left: 15px;">[Raw: 0]</span>
        `;

        // Insert in sorted order based on a defined channel priority
        const order = getChannelOrder(input.channel);
        row.dataset.order = order;
        let inserted = false;
        for (const child of container.children) {
            if (child.classList.contains('channel-group-title')) continue;
            if (parseInt(child.dataset.order) > order) {
                container.insertBefore(row, child);
                inserted = true;
                break;
            }
        }
        if (!inserted) container.appendChild(row);
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

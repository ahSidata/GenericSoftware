"""
Client for the grobro mqtt side, handling messages from/to
* growatt cloud
* growatt devices
"""

from http import client
import os
import signal
import string
import time
import struct
import logging
import ssl
import threading

import paho.mqtt.client as mqtt
from paho.mqtt.client import MQTTMessage

class Client:

    def __init__(self):
        """
        Initialize the Client with default configuration values.
        """
        # Connection parameters
        self._brokerHost = "localhost"
        self._brokerPort = 7006
        self._growattHost = "mqtt.growatt.com"
        self._growattPort = 7006
        self._clientId = ""

        # MQTT client instances
        self._clientBroker = mqtt.Client(
            client_id="relay",
        )
        self._clientBroker.tls_set(cert_reqs=ssl.CERT_NONE)
        self._clientBroker.tls_insecure_set(True)
        self._clientBroker.reconnect_delay_set(1, 120)
        self._clientBroker._reconnect_on_failure = True
        self._clientBroker.on_connect = self.__on_broker_connect
        self._clientBroker.on_disconnect = self.__on_broker_disconnect
        self._clientBroker.on_message = self.__on_broker_message
      
        # Message timing and connection monitoring
        self._lastBrokerMessageTime = 0
        self._connectionCheckInterval = 5  # Check interval in seconds
        self._connectionTimeout = 60  # Timeout in seconds
        
        # Callbacks will be set later
        self.log_callback = None
        self.dump_callback = None
        
        # Running state
        self._running = False
        
        if hasattr(self, 'log_callback') and self.log_callback:
            self.log_callback("[TRACE] Client instance initialized")
   
    def set_log_callback(self, log_callback=None):
        self.log_callback = log_callback
        
        if self.log_callback:
            self.log_callback("Logging initialized!")

    def set_dump_callback(self, dump_callback=None):
        self.dump_callback = dump_callback

    def set_options(self, options):
        if self.log_callback:
            self.log_callback(f"[TRACE] Python received options: ClientId={options.ClientId}, BrokerHost={options.BrokerHost}, BrokerPort={options.BrokerPort}, GrowattHost={options.GrowattHost}, GrowattPort={options.GrowattPort}")
        
        self._clientId = options.ClientId

        self._brokerHost = options.BrokerHost
        self._brokerPort = options.BrokerPort
        self._growattHost = options.GrowattHost
        self._growattPort = options.GrowattPort

        self._clientGrowatt = mqtt.Client(
            client_id=self._clientId,
        )
        self._clientGrowatt.tls_set(cert_reqs=ssl.CERT_NONE)
        self._clientGrowatt.tls_insecure_set(True)
        self._clientGrowatt.reconnect_delay_set(1, 120)
        self._clientGrowatt._reconnect_on_failure = True
        self._clientGrowatt.on_connect = self.__on_growatt_connect
        self._clientGrowatt.on_disconnect = self.__on_growatt_disconnect
        self._clientGrowatt.on_message = self.__on_growatt_message

    def send_msg(self, topic: string, payload: bytes, qos: int, retain: int):
        try:
            if self.log_callback:
                self.log_callback(f"Sending message: {e}")
            if self.dump_callback:
                self.dump_callback(topic, payload, qos, retain)

            self._clientBroker.publish(
                topic=topic,
                payload=payload,
                qos=qos,
                retain=retain                
            )
        except Exception as e:
            if self.log_callback:
                self.log_callback(f"Send message: {e}")

    @property
    def is_growatt_connected(self):
        return self._clientGrowatt is not None and self._clientGrowatt.is_connected()

    @property
    def is_broker_connected(self):
        return self._clientBroker is not None and self._clientBroker.is_connected()

    def start(self):
        try:
            if self.log_callback:
                self.log_callback("Python client started!")

            # Initialisiere die letzte Nachrichtenzeit
            self._lastBrokerMessageTime = time.time()

            # Start broker client
            self.__connect_to_broker()

            # Start connection monitoring thread
            self.start_monitoring()

            self._running = True
            while self._running:
                time.sleep(0.1)

            self._clientBroker.disconnect()
            self._clientGrowatt.disconnect()

            if self.log_callback:
                self.log_callback("Python client finished!")

        except Exception as e:
            if self.log_callback:
                self.log_callback(f"Python exception: {e}")

    def start_monitoring(self):
        monitor_thread = threading.Thread(target=self.__monitor_connection)
        monitor_thread.daemon = True
        monitor_thread.start()

    def stop(self):
        try:
            if self.log_callback:
                self.log_callback("Python client stoped!")
            self._running = False;

        except Exception as e:
            if self.log_callback:
                self.log_callback(f"Python exception: {e}")

    def __monitor_connection(self):
        if self.log_callback:
            self.log_callback("[TRACE] Connection monitoring started")
            
        while self._running:
            current_time = time.time()
            time_since_last_message = current_time - self._lastBrokerMessageTime
            
            if (self.is_growatt_connected and time_since_last_message > self._connectionTimeout):

                if self.log_callback:
                    self.log_callback(f"[TRACE] No broker message for {time_since_last_message:.1f} seconds. Disconnecting from Growatt.")                
                try:
                    self._clientGrowatt.loop_stop()
                    self._clientGrowatt.disconnect()
                except Exception as e:
                    if self.log_callback:
                        self.log_callback(f"[TRACE] Error disconnecting from Growatt: {e}")
            
            time.sleep(self._connectionCheckInterval)

    def __on_broker_disconnect(self, client: mqtt.Client, userdata, rc):
        try:
            if self.log_callback:
                self.log_callback("Disconnected from Broker, trying to reconnect...")
            time.sleep(30)
        except Exception as e:
                if self.log_callback:
                    self.log_callback(f"[TRACE] disconnect attempt failed: {e}")
        finally:
            # Start a new thread (task)
            task_thread = threading.Thread(target=self.__connect_to_broker())
            task_thread.start()
            
    def __on_broker_connect(self, client, userdata, flags, rc):           
        if self.log_callback:
            if rc == 0:
                self.log_callback(f"Connected to Broker at 'ah.azure.sidata.com:7006' with clientID: {self._clientBroker._client_id}")
                if self.log_callback:
                    self.log_callback("Subscribe c/#")
                self._clientBroker.subscribe("c/#")
            else:
                if self.log_callback:
                    self.log_callback(f"Connect to Broker attempt failed:: {rc}")
    
    def __connect_to_broker(self):
        try:
            if self.log_callback:
                self.log_callback(f"Connecting to Broker at '{self._brokerHost}:{self._brokerPort}' with clientID: {self._clientBroker._client_id}")

            self._clientBroker.connect(self._brokerHost, self._brokerPort, 420)
            self._clientBroker.loop_start()
            time.sleep(1)

        except Exception as e:
            if self.log_callback:
                self.log_callback(f"Python exception connecting to Growatt: {e}")

    def __on_growatt_connect(self, client, userdata, flags, rc):          
        if self.log_callback:
            if rc == 0:   
                self.log_callback(f"Connected to Growatt at '{self._growattHost}:{self._growattPort}' with clientID: {self._clientGrowatt._client_id}")
            else:
                self.log_callback(f"Connect to Growatt attempt failed: {rc}")

    def __on_growatt_disconnect(self, client, userdata, rc):
        try:
            if self.log_callback:   
                self.log_callback("Disconnected from Growatt.")

        except Exception as e:
            if self.log_callback:
                self.log_callback(f"Disconnect attempt failed: {e}")
            
    def __connect_to_growatt(self):
        try:
            if self.log_callback:
                self.log_callback(f"Connecting to Growatt at '{self._growattHost}:{self._growattPort}' with clientID: {self._clientGrowatt._client_id}")
            self._clientGrowatt.connect(self._growattHost,  self._growattPort, 420)
            self._clientGrowatt.loop_start()
            time.sleep(1)

        except Exception as e:
            if self.log_callback:
                self.log_callback(f"Python exception connecting to Growatt: {e}")

    def __on_broker_message(self, client, userdata, msg: MQTTMessage):
        try:
            # Aktualisiere den Zeitstempel des letzten Pakets
            self._lastBrokerMessageTime = time.time()

            # Stelle sicher, dass Growatt verbunden ist, bevor wir Nachrichten weiterleiten
            if not self.is_growatt_connected:
                if self.log_callback:
                    self.log_callback("Broker message received. Reconnecting to Growatt.")
                self.__connect_to_growatt()           

            # Nur weiterleiten, wenn die Verbindung erfolgreich hergestellt wurde
            if self.is_growatt_connected:
                self._clientGrowatt.publish(
                    msg.topic,
                    payload=msg.payload,
                    qos=msg.qos,
                    retain=msg.retain,
                )

            if self.dump_callback:
                self.dump_callback(msg.topic, msg.payload, msg.qos, msg.retain, msg.state, msg.dup, msg.mid)
        except Exception as e:
            if self.log_callback:
                self.log_callback(f"Error Processing message: {e}")

    def __on_growatt_message(self, client, userdata, msg: MQTTMessage):
        try:
            if self.dump_callback:
                self.dump_callback(msg.topic, msg.payload, msg.qos, msg.retain, msg.state, msg.dup, msg.mid)

            device_id = msg.topic.split("/")[-1]

            if self.is_broker_connected:
                self._clientBroker.publish(
                    msg.topic.split("/")[0] + "/33/" + device_id,
                    payload=msg.payload,
                    qos=msg.qos,
                    retain=msg.retain,
                )
        except Exception as e:
            if self.log_callback:
                self.log_callback(f"Forwarding message: {e}")
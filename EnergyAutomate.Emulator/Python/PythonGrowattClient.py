"""
Client for the grobro mqtt side, handling messages from/to
* growatt cloud
* growatt devices
"""

from concurrent.futures import ThreadPoolExecutor
from http import client
import os
import signal
import string
import time
import struct
import logging
import ssl
import threading
import datetime

import paho.mqtt.client as mqtt
from paho.mqtt.client import MQTTMessage

class Client:

    def __init__(self):
        """
        Initialize the Client with default configuration values.
        """
        # Thread-Pool mit begrenzter Anzahl von Threads
        self._thread_pool = ThreadPoolExecutor(max_workers=10)

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
        self._connectionCheckInterval = 1  # Check interval in seconds
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
        self._clientGrowatt.reconnect_delay_set(5, 120)
        self._clientGrowatt._reconnect_on_failure = True
        self._clientGrowatt.on_connect = self.__on_growatt_connect
        self._clientGrowatt.on_disconnect = self.__on_growatt_disconnect
        self._clientGrowatt.on_message = self.__on_growatt_message

    def send_msg(self, topic: string, payload: bytes, qos: int, retain: int):
        try:
            self.log(f"Sending message: {e}")
            self.dump(topic, payload, qos, retain)
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
            self.log("Python client started!")

            # Initialisiere die letzte Nachrichtenzeit
            self._lastBrokerMessageTime = time.time()

            # Start broker client
            self.__connect_to_broker()

            # Start connection monitoring thread
            self.__monitor_connection()

            self._clientBroker.disconnect()
            self._clientGrowatt.disconnect()

            self.log("Python client finished!")

        except Exception as e:
            self.log(f"Python exception: {e}")

    def stop(self):
        try:
            self.log("Python client stoped!")
            self._running = False;

        except Exception as e:
            self.log(f"Python exception: {e}")

    def __monitor_connection(self):
        self.log("[TRACE] Connection monitoring started")
        self._running = True
        while self._running:
            current_time = time.time()
            time_since_last_message = current_time - self._lastBrokerMessageTime
            
            if (self.is_growatt_connected and time_since_last_message > self._connectionTimeout):

                # Konvertiere Unix-Zeit in lesbaren Zeit-String
                last_message_time_str = datetime.datetime.fromtimestamp(self._lastBrokerMessageTime).strftime('%Y-%m-%d %H:%M:%S')

                self.log(f"No broker message for {time_since_last_message:.1f} seconds. Last msg: {last_message_time_str} Disconnecting from Growatt.")                
                try:
                    self._clientGrowatt.loop_stop()
                    self._clientGrowatt.disconnect()
                except Exception as e:
                    self.log(f"[TRACE] Error disconnecting from Growatt: {e}")
            
            time.sleep(self._connectionCheckInterval)

    def __on_broker_disconnect(self, client: mqtt.Client, userdata, rc):
        try:
            self.log("Disconnected from Broker, trying to reconnect...")
        except Exception as e:
                self.log(f"[TRACE] disconnect attempt failed: {e}")
        finally:
            try:
                threading.Thread(target=self.__connect_to_broker()).start()
            except RuntimeError as e:
                self.log(f"[ERROR] Failed to start broker thread: {e}")
            
    def __on_broker_connect(self, client, userdata, flags, rc):           
        if rc == 0:
            self.log(f"Connected to Broker at 'ah.azure.sidata.com:7006' with clientID: {self._clientBroker._client_id}")
            self.log("Subscribe c/#")
            self._clientBroker.subscribe("c/#")
        else:
            self.log(f"Connect to Broker attempt failed:: {rc}")
    
    def __connect_to_broker(self):
        try:
            self.log(f"Connecting to Broker at '{self._brokerHost}:{self._brokerPort}' with clientID: {self._clientBroker._client_id}")
            self._clientBroker.connect(self._brokerHost, self._brokerPort, 60)
            self._clientBroker.loop_start()

        except Exception as e:
            self.log(f"Python exception connecting to Growatt: {e}")

    def __on_growatt_connect(self, client, userdata, flags, rc):                 
        if rc == 0:   
            self.log(f"Connected to Growatt at '{self._growattHost}:{self._growattPort}' with clientID: {self._clientGrowatt._client_id}")
        else:
            self.log(f"Connect to Growatt attempt failed: {rc}")

    def __on_growatt_disconnect(self, client, userdata, rc):
        try:
            self.log("Disconnected from Growatt.")

        except Exception as e:
            self.log(f"Disconnect attempt failed: {e}")
            
    def __connect_to_growatt(self):
        try:
            self.log(f"Connecting to Growatt at '{self._growattHost}:{self._growattPort}' with clientID: {self._clientGrowatt._client_id}")
            self._clientGrowatt.connect(self._growattHost,  self._growattPort, 420)
            self._clientGrowatt.loop_start()

        except Exception as e:
            self.log(f"Python exception connecting to Growatt: {e}")

    def __on_broker_message(self, client, userdata, msg: MQTTMessage):
        try:
            # Aktualisiere den Zeitstempel des letzten Pakets
            self._lastBrokerMessageTime = time.time()

            # Stelle sicher, dass Growatt verbunden ist, bevor wir Nachrichten weiterleiten
            if not self.is_growatt_connected:
                self.log("Broker message received. Reconnecting to Growatt.")
                try:
                    threading.Thread(target=self.__connect_to_growatt()).start()                   
                except RuntimeError as e:
                    self.log(f"[ERROR] Failed to start growatt thread: {e}")
         
            # Nur weiterleiten, wenn die Verbindung erfolgreich hergestellt wurde
            if self.is_growatt_connected:
                self._clientGrowatt.publish(
                    msg.topic,
                    payload=msg.payload,
                    qos=msg.qos,
                    retain=msg.retain,
                )

            self.dump(msg.topic, msg.payload, msg.qos, msg.retain, msg.state, msg.dup, msg.mid)
        except Exception as e:
            self.log(f"Error Processing message: {e}")

    def __on_growatt_message(self, client, userdata, msg: MQTTMessage):
        try:
            device_id = msg.topic.split("/")[-1]

            if self.is_broker_connected:
                self._clientBroker.publish(
                    msg.topic.split("/")[0] + "/33/" + device_id,
                    payload=msg.payload,
                    qos=msg.qos,
                    retain=msg.retain,
                )

            self.dump(msg.topic, msg.payload, msg.qos, msg.retain, msg.state, msg.dup, msg.mid)
        except Exception as e:
            self.log(f"Forwarding message: {e}")

    def log(self, msg):
        """
        Logs a message in a separate thread.
        """
        # Define the target function for the thread
        def log_task():
            if self.log_callback:
                self.log_callback(msg)
        # Start the thread with the target function
        try:
            self._thread_pool.submit(log_task)
        except RuntimeError as e:
            self.log(f"[ERROR] Failed to start log thread: {e}")
                
    def dump(self, topic, payload, qos, retain, state, dup, mid):
        """
        Logs a message in a separate thread.
        """
        # Define the target function for the thread
        def dump_task():
            if self.dump_callback:
                self.dump_callback(topic, payload, qos, retain, state, dup, mid)

        # Start the thread with the target function
        try:
            self._thread_pool.submit(dump_task)
        except RuntimeError as e:
            self.log(f"[ERROR] Failed to start dump thread: {e}")
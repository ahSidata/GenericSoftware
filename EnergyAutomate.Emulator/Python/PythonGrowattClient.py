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

    _brokerHost = "localhost"
    _brokerPort = 7006
    _growattHost = "mqtt.growatt.com"
    _growattPort = 7006
    _clientId = ""

    _clientBroker: mqtt.Client
    _clientGrowatt: mqtt.Client

    _clientBrokerLooping = False
    _clientGrowattLooping = False
   
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

    def set_clientid(self, clientId):
        if self.log_callback:
            self.log_callback(f"[TRACE] Python received clientid: {clientId}")
        self._clientId = clientId

    def send_msg(self, topic: string, payload: bytes, qos: int, retain: int):
        try:
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

    def run(self):
        try:
            if self.log_callback:
                self.log_callback("Python client started!")

            # Start a new thread (task)
            broker_task_thread = threading.Thread(target=self.__connect_to_broker())
            broker_task_thread.start()

            # Start a new thread (task)
            growatt_task_thread = threading.Thread(target=self.__connect_to_growatt_server())
            growatt_task_thread.start()

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

    def __on_broker_disconnect(self, client: mqtt.Client, userdata, rc):
        try:
            if self.log_callback:
                self.log_callback("Disconnected from Broker, trying to reconnect...")
            if self._clientBrokerLooping:
                self._clientBroker.loop_stop()
                self._clientBrokerLooping = False
        except Exception as e:
                if self.log_callback:
                    self.log_callback(f"[TRACE] disconnect attempt failed: {e}")
        finally:
            # Start a new thread (task)
            task_thread = threading.Thread(target=self.__connect_to_broker())
            task_thread.start()
            
    def __connect_to_broker(self):           
        try:
            if self.log_callback:
                self.log_callback(f"Connecting to Broker at 'ah.azure.sidata.com:7006' with clientID: {self._clientId}")

            self._clientBroker = mqtt.Client(client_id="relay")
            self._clientBroker.tls_set(cert_reqs=ssl.CERT_NONE)
            self._clientBroker.tls_insecure_set(True)
            self._clientBroker.on_disconnect = self.__on_broker_disconnect
            self._clientBroker.on_message = self.__on_message
            self._clientBroker.connect(self._brokerHost, self._brokerPort, 60)
            self._clientBroker.loop_start()
            self._clientBrokerLooping = True

            wait_time = 0
            while not self._clientBroker.is_connected():
                time.sleep(0.1)
                wait_time += 0.1
                if wait_time >= 5:
                    if self.log_callback:
                        self.log_callback("[TRACE] Broker connection timeout.")
                    break

            if self.log_callback:
                self.log_callback("[TRACE] Successfully connected to Broker.")
                self.log_callback("Subscribe c/#")

            self._clientBroker.subscribe("c/#")

        except Exception as e:
            if self.log_callback:
                self.log_callback(f"Python exception: {e}")
        finally:
            if not self._clientBroker.is_connected():
                if self._clientBrokerLooping:
                    self._clientBroker.loop_stop()
                    self._clientBrokerLooping = False
                time.sleep(30)
                # Start a new thread (task)
                task_thread = threading.Thread(target=self.__connect_to_broker())
                task_thread.start()

    def __on_growatt_disconnect(self, client, userdata, rc):
        try:
            if self.log_callback:
                self.log_callback("Disconnected from Growatt, trying to reconnect...")
            if self._clientGrowattLooping:
                self._clientGrowatt.loop_stop()
                self._clientGrowattLooping = False
        except Exception as e:
            if self.log_callback:
                self.log_callback(f"[TRACE] disconnect attempt failed: {e}")
        finally:            
            # Start a new thread (task)
            task_thread = threading.Thread(target=self.__connect_to_growatt_server())
            task_thread.start()
            
    def __connect_to_growatt_server(self):
        try:
            if self.log_callback:
                self.log_callback(f"Connecting to Growatt at 'mqtt.growatt.com:7006' with clientID: {self._clientId}")

            self._clientGrowatt = mqtt.Client(client_id=self._clientId)
            self._clientGrowatt.tls_set(cert_reqs=ssl.CERT_NONE)
            self._clientGrowatt.tls_insecure_set(True)
            self._clientGrowatt.on_message = self.__on_message_forward_client
            self._clientGrowatt.on_disconnect = self.__on_growatt_disconnect

            self._clientGrowatt.connect(self._growattHost,  self._growattPort, 60)

            self._clientGrowatt.loop_start()
            self._clientGrowattLooping = True

            wait_time = 0
            while not self._clientGrowatt.is_connected():
                time.sleep(0.1)
                wait_time += 0.1
                if wait_time >= 5:
                    if self.log_callback:
                        self.log_callback("[TRACE] Growatt connection timeout.")
                    break

            if self.log_callback:
                self.log_callback("[TRACE] Successfully connected to Growatt.")  

        except Exception as e:
            if self.log_callback:
                self.log_callback(f"Python exception: {e}")
        finally:
            if not self._clientGrowatt.is_connected():
                if self._clientGrowattLooping:
                    self._clientGrowatt.loop_stop()
                    self._clientGrowattLooping = False
                time.sleep(30)
                # Start a new thread (task)
                task_thread = threading.Thread(target=self.__connect_to_growatt_server())
                task_thread.start()

    def __on_message(self, client, userdata, msg: MQTTMessage):
        try:
            if self.dump_callback:
                self.dump_callback(msg.topic, msg.payload, msg.qos, msg.retain, msg.state, msg.dup, msg.mid)

            self._clientGrowatt.publish(
                msg.topic,
                payload=msg.payload,
                qos=msg.qos,
                retain=msg.retain,
            )
        except Exception as e:

            self.log_callback(f"Error Processing message: {e}")

    def __on_message_forward_client(self, client, userdata, msg: MQTTMessage):
        try:
            if self.dump_callback:
                self.dump_callback(msg.topic, msg.payload, msg.qos, msg.retain, msg.state, msg.dup, msg.mid)

            device_id = msg.topic.split("/")[-1]

            # We need to publish the messages from Growatt on the Topic
            # s/33/{deviceid}. Growatt sends them on Topic s/{deviceid}
            self._clientBroker.publish(
                msg.topic.split("/")[0] + "/33/" + device_id,
                payload=msg.payload,
                qos=msg.qos,
                retain=msg.retain,
            )
        except Exception as e:
            if self.log_callback:
                self.log_callback(f"Forwarding message: {e}")
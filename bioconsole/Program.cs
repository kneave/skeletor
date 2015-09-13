using Microsoft.Kinect;
using NetMQ;
using SlimDX;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace bioconsole
{
    class Program
    {
        private static KinectSensor sensor = null;
        private static Body[] bodies = null;
        private static List<Tuple<JointType, JointType>> bones;
        private static string name = string.Empty;

        private enum VerificationState { StartEnrolment, CollectingData, Verifying }
        private static VerificationState verificationState = VerificationState.Verifying;

        private static SQLiteConnection m_dbConnection;
        private static List<Dictionary<string, float>> enrolmentData = new List<Dictionary<string, float>>();
        
        private static int enrolmentModeCounter = 0;

        static void Main(string[] args)
        {
            //  Create the list of joints
            //GenerateBones();

            connectToDatabase();

            sensor = KinectSensor.GetDefault();
            sensor.Open();

            BodyFrameReader bfr = sensor.BodyFrameSource.OpenReader();
            bfr.FrameArrived += bfr_FrameArrived;

            Console.WriteLine("Running.");
            
            using (NetMQContext context = NetMQContext.Create())
            {
                Task serverTask = Task.Factory.StartNew(() => StartServerNetMq(context));
                Task.WaitAll(serverTask);
            }

            Thread.Sleep(Timeout.Infinite);
        }

        private static void StartServerNetMq(NetMQContext context)
        {
            using (NetMQSocket serverSocket = context.CreateResponseSocket())
            {
                serverSocket.Options.SendTimeout = TimeSpan.FromMilliseconds(60000);

                serverSocket.Bind(string.Format("tcp://*:{0}", 2804));
                string message = string.Empty;
                string retMsg = string.Empty;

                Console.WriteLine("Server started;");

                while (true)
                {
                    //log.WriteLog("Received command {0}, {1} bytes returned.", message, results.Length);
                    try
                    {
                        message = serverSocket.ReceiveString();
                        Console.WriteLine("Message: {0} received.", message);
                        if(message == "Who's there?")
                        {
                            retMsg = name;
                        }

                        serverSocket.Send(retMsg);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error sending data; message: {0}, error: {1}", message, e);
                        serverSocket.Send(string.Empty);
                    }
                }
            }
        }

        private static void connectToDatabase()
        {
            m_dbConnection = new SQLiteConnection("Data Source=enrolment.db;Version=3;");
            m_dbConnection.Open();
        }

        private static float BoneLength(IReadOnlyDictionary<JointType, Joint> joints, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            float boneLength = -1;

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked ||
                joint1.TrackingState == TrackingState.NotTracked)
            {
                return boneLength;
            }

            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                Vector3 jointPos0, jointPos1;
                jointPos0 = new Vector3(joint0.Position.X, joint0.Position.Y, joint0.Position.Z);
                jointPos1 = new Vector3(joint1.Position.X, joint1.Position.Y, joint1.Position.Z);
                boneLength = Vector3.Distance(jointPos0, jointPos1);
            }

            return boneLength;
        }

        private static void GenerateBones()
        {
            // a bone defined as a line between two joints
            bones = new List<Tuple<JointType, JointType>>();

            // Torso
            bones.Add(new Tuple<JointType, JointType>(JointType.Head, JointType.Neck));
            bones.Add(new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder));
            bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid));
            bones.Add(new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase));
            bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight));
            bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft));
            bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight));
            bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft));

            // Right Arm
            bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
            bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
            bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));
            bones.Add(new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight));
            bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight));

            // Left Arm
            bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft));
            bones.Add(new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft));
            bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft));
            bones.Add(new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft));
            bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.ThumbLeft));

            // Right Leg
            bones.Add(new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight));
            bones.Add(new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight));
            bones.Add(new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight));

            // Left Leg
            bones.Add(new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft));
            bones.Add(new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft));
            bones.Add(new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft));
        }

        static void bfr_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (bodies == null)
                    {
                        bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                foreach (Body body in bodies)
                {
                    if (body.IsTracked)
                    {
                        IReadOnlyDictionary<JointType, Joint> joints = body.Joints;
                        Dictionary<string, float> person = new Dictionary<string, float>();
                        
                        person.Add("neck", BoneLength(joints, JointType.Neck, JointType.Head));
                        person.Add("left_shin", BoneLength(joints, JointType.AnkleLeft, JointType.KneeLeft));
                        person.Add("right_shin", BoneLength(joints, JointType.AnkleRight, JointType.KneeRight));
                        person.Add("left_thigh", BoneLength(joints, JointType.KneeLeft, JointType.HipLeft));
                        person.Add("right_thigh", BoneLength(joints, JointType.KneeRight, JointType.HipRight));
                        person.Add("forearm_left", BoneLength(joints, JointType.HandLeft, JointType.ElbowLeft));
                        person.Add("forearm_right", BoneLength(joints, JointType.HandRight, JointType.ElbowRight));
                        person.Add("upperarm_left", BoneLength(joints, JointType.ShoulderLeft, JointType.ElbowLeft));
                        person.Add("upperarm_right", BoneLength(joints, JointType.ShoulderRight, JointType.ElbowRight));
                        person.Add("spine_lower", BoneLength(joints, JointType.SpineBase, JointType.SpineMid));
                        person.Add("spine_upper", BoneLength(joints, JointType.SpineShoulder, JointType.SpineMid));
                        person.Add("shoulder_width", BoneLength(joints, JointType.ShoulderLeft, JointType.ShoulderRight));
                        person.Add("hip_width", BoneLength(joints, JointType.HipLeft, JointType.HipRight));

                        float height = person["left_shin"] + person["left_thigh"] + person["spine_lower"] + person["spine_upper"] + person["neck"];
                        person.Add("height", height);
                        
                        string[] limbs = { "left_thigh", "right_thigh", "left_shin", "right_shin", "spine_upper", "spine_lower", "forearm_left", "forearm_right", "upperarm_left", "upperarm_right", "neck", "shoulder_width", "hip_width", "height" };

                        if (body.HandLeftState == HandState.Closed && body.HandRightState == HandState.Open && verificationState == VerificationState.Verifying)
                        {
                            enrolmentModeCounter++;

                            if (enrolmentModeCounter >= 20)
                            {
                                Console.WriteLine("Starting enrolment;");
                                verificationState = VerificationState.StartEnrolment;
                                enrolmentData.Clear();
                            }
                        }
                        else
                        {
                            enrolmentModeCounter = 0;
                        }

                        if (body.HandLeftState == HandState.Open && body.HandRightState == HandState.Closed && verificationState == VerificationState.CollectingData)
                        {
                            Dictionary<string, float> model = new Dictionary<string, float>();

                            if (enrolmentData.Count >= 50)
                            {
                                Console.WriteLine("Enrolment complete.");
                                verificationState = VerificationState.Verifying;

                                model.Clear();

                                //  add up the values for all parts
                                foreach (var datapoint in enrolmentData)
                                {
                                    bool voidReading = false;

                                    foreach (var part in datapoint.Keys)
                                    {
                                        if(datapoint[part] == -1)
                                        {
                                            voidReading = true;
                                            break;
                                        }
                                    }

                                    if (voidReading)
                                    {
                                        continue;
                                    }
                                    
                                    foreach (var part in person.Keys)
                                    {
                                        if (model.ContainsKey(part))
                                        {
                                            model[part] += datapoint[part];
                                        }
                                        else
                                        {
                                            model.Add(part, datapoint[part]);
                                        }
                                    }
                                }

                                //  calculate the average
                                //  the +1 is because the last reading was still present in person

                                foreach (string limb in limbs)
                                {
                                    //  Convert from meters to cm
                                    model[limb] = model[limb] * 100;
                                    model[limb] = model[limb] / enrolmentData.Count;
                                }

                                //  Save the averaged data
                                SaveData(model,name);

                            }
                            else
                            {
                                Console.WriteLine("Saving data at {0}", DateTime.Now.ToFileTime());
                                //SaveData(person, name);
                                enrolmentData.Add(person);
                            }
                        }

                        switch (verificationState)
                        {
                            case VerificationState.StartEnrolment:
                                Console.WriteLine("Please enter your name:");
                                name = Console.ReadLine();
                                verificationState = VerificationState.CollectingData;
                                break;
                            case VerificationState.Verifying:
                                VerifySkeleton(person, limbs);
                                break;
                        }

                        //Console.WriteLine("{0},{1},{2}", person["neck"], person["shin_left"], person["shin_right"]);
                    }
                }
            }
        }

        private static void VerifySkeleton(Dictionary<string, float> person, string[] limbs)
        {
            string getModels = "select * from people where " +
                "(left_thigh between @left_thigh_lower and @left_thigh_upper) or " +
                "(right_thigh between @right_thigh_lower and @right_thigh_upper) or " +
                "(left_shin between @left_shin_lower and @left_shin_upper) or " +
                "(right_shin between @right_shin_lower and @right_shin_upper) or " +
                "(spine_upper between @spine_upper_lower and @spine_upper_upper) or " +
                "(spine_lower between @spine_lower_lower and @spine_lower_upper) or " +
                "(forearm_left between @forearm_left_lower and @forearm_left_upper) or " +
                "(forearm_right between @forearm_right_lower and @forearm_right_upper) or" +
                "(upperarm_left between @upperarm_left_lower and @upperarm_left_upper) or" +
                "(upperarm_right between @upperarm_right_lower and @upperarm_right_upper) or " +
                "(neck between @neck_lower and @neck_upper) or " +
                "(shoulder_width between @shoulder_width_lower and @shoulder_width_upper) or " + 
                "(hip_width between @hip_width_lower and @hip_width_upper) or " +
                "(height between @height_lower and @height_upper)";

            using (SQLiteCommand command = new SQLiteCommand(m_dbConnection))
            {
                command.CommandText = getModels;

                foreach (string limb in limbs)
                {
                    command.Parameters.Add(limb + "_lower", System.Data.DbType.Single);
                    command.Parameters[limb + "_lower"].Value = (person[limb] * 100) - 0.1f;

                    command.Parameters.Add(limb + "_upper", System.Data.DbType.Single);
                    command.Parameters[limb + "_upper"].Value = (person[limb] * 100) + 0.1f;
                }

                SQLiteDataReader reader = command.ExecuteReader();

                Dictionary<string, Dictionary<string, double>> results = new Dictionary<string, Dictionary<string, double>>();
                if (reader.HasRows)
                {
                    //Console.Write("Name(s) returned: ");
                    while (reader.Read())
                    {
                        string resName = (string)reader["name"];
                        if (!results.ContainsKey(resName))
                        {
                            results.Add(resName, new Dictionary<string, double>());
                            try
                            {
                                results[resName].Add(limbs[0], (double)reader[limbs[0]]);
                                results[resName].Add(limbs[1], (double)reader[limbs[1]]);
                                results[resName].Add(limbs[2], (double)reader[limbs[2]]);
                                results[resName].Add(limbs[3], (double)reader[limbs[3]]);
                                results[resName].Add(limbs[4], (double)reader[limbs[4]]);
                                results[resName].Add(limbs[5], (double)reader[limbs[5]]);
                                results[resName].Add(limbs[6], (double)reader[limbs[6]]);
                                results[resName].Add(limbs[7], (double)reader[limbs[7]]);
                                results[resName].Add(limbs[8], (double)reader[limbs[8]]);
                                results[resName].Add(limbs[9], (double)reader[limbs[9]]);
                                results[resName].Add(limbs[10], (double)reader[limbs[10]]);
                                results[resName].Add(limbs[11], (double)reader[limbs[11]]);
                                results[resName].Add(limbs[12], (double)reader[limbs[12]]);
                                results[resName].Add(limbs[13], (double)reader[limbs[13]]);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex);
                            }
                        }
                        //Console.Write("{0}, ", resName);
                    }
                    //Console.WriteLine();

                    //  Iterate through the results dictionary to compare values against detected
                    //  count how many are close matches

                    foreach (string key in results.Keys)
                    {
                        int featureCount = 0;
                        foreach (string limb in results[key].Keys)
                        {
                            float detectedValue = person[limb] * 100;
                            double queryValue = results[key][limb];

                            float lower, upper;
                            lower = detectedValue - 0.5f;
                            upper = detectedValue + 0.5f;

                            if ((lower < queryValue) && (upper > queryValue))
                                featureCount++;
                        }
                        //Console.WriteLine("Feature count match for {0} is {1}", key, featureCount);

                        if(featureCount >= 7)
                        {
                            Console.WriteLine("Very likely {0} detected, {1} features matched at {2}.", key, featureCount, DateTime.Now.ToFileTime());
                            name = key;
                        }
                    }
                }
            }
        }

        private static void SaveData(Dictionary<string, float> person, string name)
        {
            string sql = "insert into people (name, left_thigh, right_thigh, left_shin, right_shin, spine_upper, spine_lower, forearm_left, forearm_right, upperarm_left, upperarm_right, neck, shoulder_width, hip_width, height)" +
                "values (@name, @left_thigh, @right_thigh, @left_shin, @right_shin, @spine_upper, @spine_lower, @forearm_left, @forearm_right, @upperarm_left, @upperarm_right, @neck, @shoulder_width, @hip_width, @height)";

            //  left_thigh, right_thigh, left_shin, right_shin, spine_upper, spine_lower, forearm_left, forearm_right, upperarm_left, upperarm_right, neck

            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);

            command.Parameters.Add("name", System.Data.DbType.String);
            command.Parameters["name"].Value = name;

            foreach (string bone in person.Keys)
            {
                command.Parameters.Add(bone, System.Data.DbType.Single);
                command.Parameters[bone].Value = person[bone];
            }

            command.ExecuteNonQuery();
        }
    }
}
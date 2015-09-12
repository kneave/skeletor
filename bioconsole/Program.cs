using Microsoft.Kinect;
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

        private static SQLiteConnection m_dbConnection;

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
            Console.ReadLine();
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

                        string sql = "insert into people (name, left_thigh, right_thigh, left_shin, right_shin, spine_upper, spine_lower, forearm_left, forearm_right, upperarm_left, upperarm_right, neck)" +
                            "values (@name, @left_thigh, @right_thigh, @left_shin, @right_shin, @spine_upper, @spine_lower, @forearm_left, @forearm_right, @upperarm_left, @upperarm_right, @neck)";

                        //  left_thigh, right_thigh, left_shin, right_shin, spine_upper, spine_lower, forearm_left, forearm_right, upperarm_left, upperarm_right, neck

                        SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);

                        command.Parameters.Add("name", System.Data.DbType.String);
                        command.Parameters["name"].Value = "Wayne";

                        foreach (string bone in person.Keys)
                        {
                            command.Parameters.Add(bone, System.Data.DbType.Single);
                            command.Parameters[bone].Value = person[bone];
                        }

                        command.ExecuteNonQuery();
                           
                        //Console.WriteLine("{0},{1},{2}", person["neck"], person["shin_left"], person["shin_right"]);
                    }
                }
            }
        }
    }
}